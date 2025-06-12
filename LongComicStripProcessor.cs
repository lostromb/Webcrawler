using Durandal.Common.Compression;
using Durandal.Common.Compression.Zip;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.Tasks;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WebCrawler
{
    public static class LongComicStripProcessor
    {
        private const int PREFERRED_PAGE_HEIGHT_PX = 1100; // Desired height for non-clean page breaks
        private const int AUTOSTITCH_PAGE_HEIGHT_PX = 1300; // Max height when combining multiple small clean segments
        private const int MAX_PAGE_HEIGHT_PX = 1600; // Defines the maximum size a single page can be
        private const int MIN_PAGE_HEIGHT_PX = 350; // Defines the minimum size a single page can be
        private const float BLANK_LINE_THRESH = 0.99f; // Threshold for detecting lines in the image where there is no variance - used to detect page break locations
        private const float BLANK_PAGE_TOTAL_VARIANCE_THRESH = 0.98f; // An entire page must have "blankness" metric lower than this threshold to avoid being dropped
        private const float BLANK_PAGE_SINGLE_LINE_VARIANCE_THRESH = 0.95f; // All single lines on the page must have "blankness" metric lower than this threshold to avoid being dropped
        private const int RGB_THRESH_FOR_DISTINCT_SUBPAGE = 10; // Higher number = less sensitive detection of distinct subpages
        private const int DESIRED_PAGE_PADDING_PX = 50; // amount of whitespace padding we'd like to have at top + bottom of each page
        private const int MIN_VIABLE_PAGE_HEIGHT = 10; // height that an image needs to be to be treated as an actual page, otherwise it is discarded
        private const int SUBDIVIDED_PAGE_OVERLAP = 50; // number of pixels to overlap when subdividing a page along non-clean lines. This helps prevent text and other details from being lost on breaks

        // TODO: Shrink down excessive blank space between pages so it's always like 100px or something?

        public static async Task<List<VirtualPath>> PostprocessImageStrips(
            ILogger logger,
            IFileSystem fileSystem,
            IEnumerable<VirtualPath> inputFiles,
            VirtualPath outputDirectory)
        {
            logger.Log("Postprocessing image strips...", LogLevel.Vrb);
            List<VirtualPath> outputImageNames = new List<VirtualPath>();

            List<Image> allImages = new List<Image>();
            foreach (VirtualPath inputImage in inputFiles)
            {
                using (Stream imageStream = await fileSystem.OpenStreamAsync(inputImage, FileOpenMode.Open, FileAccessMode.Read))
                {
                    logger.Log("Reading input image " + inputImage.Name, LogLevel.Vrb);
                    allImages.Add(Image.FromStream(imageStream));
                }
            }

            if (allImages.Count == 0)
            {
                logger.Log("No images to process!", LogLevel.Wrn);
                return outputImageNames;
            }

            Bitmap hugeBuffer = await AssembleImageStripFromFiles(logger, allImages, fileSystem, outputDirectory);

            //await WriteDebugImageInSegments(hugeBuffer, 16000, "large_", fileSystem, outputDirectory);

            IList<Bitmap> subImages = await SplitImageAtDistinctBreaks(logger, hugeBuffer, fileSystem, outputDirectory);

            int outputImageIdx = 0;
            int subImageIdx = 0;
            List<Bitmap> allOutputImages = new List<Bitmap>();
            foreach (Bitmap subImage in subImages)
            {
                logger.Log("Processing subimage " + subImageIdx, LogLevel.Vrb);
                IList<Bitmap> pages = await SplitSingleImageIntoPages(logger, subImage, fileSystem, outputDirectory);
                allOutputImages.AddRange(pages);
                subImageIdx++;
            }

            logger.Log("Combining sequential small pages - input size " + allOutputImages.Count + " pages", LogLevel.Vrb);
            IList<Bitmap> combinedFinalImages = CombineSequentialSmallPages(logger, allOutputImages);
            logger.Log("Finished combining small pages - output size " + combinedFinalImages.Count + " pages", LogLevel.Vrb);

            foreach (Bitmap finalOutputPage in combinedFinalImages)
            {
                VirtualPath outputFileName = outputDirectory.Combine(string.Format("page_{0:D3}.jpg", outputImageIdx));
                using (Stream imageStream = await fileSystem.OpenStreamAsync(outputFileName, FileOpenMode.Create, FileAccessMode.Write))
                {
                    logger.Log("Saving slice as " + outputFileName.FullName, LogLevel.Vrb);
                    finalOutputPage.Save(imageStream, ImageFormat.Jpeg);
                    outputImageNames.Add(outputFileName);
                }

                outputImageIdx++;
            }

            return outputImageNames;
        }

        private static IList<Bitmap> CombineSequentialSmallPages(ILogger logger, IList<Bitmap> inputImages)
        {
            List<Bitmap> returnVal = new List<Bitmap>();
            if (inputImages == null || inputImages.Count == 0)
            {
                return returnVal;
            }

            int sourceIdx = 0;
            while (sourceIdx < inputImages.Count)
            {
                int pageWidth = inputImages[sourceIdx].Width;
                int pageHeight = inputImages[sourceIdx].Height;
                int toIdx = sourceIdx + 1;
                while (toIdx < inputImages.Count)
                {
                    if (inputImages[toIdx].Height + pageHeight > AUTOSTITCH_PAGE_HEIGHT_PX)
                    {
                        break;
                    }

                    pageHeight += inputImages[toIdx].Height;
                    toIdx++;
                }

                if (toIdx == sourceIdx + 1)
                {
                    // Pass page across
                    returnVal.Add(inputImages[sourceIdx]);
                }
                else
                {
                    // Stitch multiple pages
                    logger.Log("Stitching multiple small pages together", LogLevel.Vrb);
                    Bitmap stitchedPage = new Bitmap(pageWidth, pageHeight, PixelFormat.Format24bppRgb);
                    using (Graphics bufferGraphics = Graphics.FromImage(stitchedPage))
                    {
                        int y = 0;
                        for (int imgIdx = sourceIdx; imgIdx < toIdx; imgIdx++)
                        {
                            // Scale the image to the buffer width using bicubic interpolation
                            bufferGraphics.DrawImage(inputImages[imgIdx], 0, y);
                            y += inputImages[imgIdx].Height;
                        }

                        returnVal.Add(stitchedPage);
                    }
                }

                sourceIdx = toIdx;
            }

            return returnVal;
        }

        private static async Task<Bitmap> AssembleImageStripFromFiles(ILogger logger, IList<Image> inputImages, IFileSystem fileSystem, VirtualPath outputDirectory)
        {
            if (inputImages.Count == 0)
            {
                return new Bitmap(0, 0, PixelFormat.Format24bppRgb);
            }

            int bufferWidth;
            List<int> imageWidths = new List<int>();
            foreach (Image img in inputImages)
            {
                imageWidths.Add(img.Width);
            }

            // buffer width is median image width
            imageWidths.Sort();
            bufferWidth = imageWidths[imageWidths.Count / 2];

            // Now we have to calculate the height based on scaled size of each subimage
            int bufferHeight = 0;
            foreach (Image img in inputImages)
            {
                if (img.Width != bufferWidth)
                {
                    bufferHeight += (int)((float)img.Height * (float)bufferWidth / (float)img.Width);
                }
                else
                {
                    bufferHeight += img.Height;
                }
            }

            logger.Log("Creating buffer with width = " + bufferWidth + " and height = " + bufferHeight, LogLevel.Vrb);
            Bitmap buffer = new Bitmap(bufferWidth, bufferHeight, PixelFormat.Format24bppRgb);
            using (Graphics bufferGraphics = Graphics.FromImage(buffer))
            {
                bufferGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                int y = 0;
                int stitchBufIdx = 0;
                foreach (Image image in inputImages)
                {
                    if (image.Width != bufferWidth)
                    {
                        // Scale the image to the buffer width using bicubic interpolation
                        int scaledImageHeight = (int)((float)image.Height * (float)bufferWidth / (float)image.Width);
                        logger.Log(string.Format("Scaling raw image #{0} from {1}x{2} to {3}x{4}", stitchBufIdx, image.Width, image.Height, bufferWidth, scaledImageHeight), LogLevel.Vrb);
                        bufferGraphics.DrawImage(
                            image,
                            new RectangleF(0, y, bufferWidth, scaledImageHeight),
                            new RectangleF(0, 0, image.PhysicalDimension.Width, image.PhysicalDimension.Height),
                            GraphicsUnit.Pixel);
                        y += scaledImageHeight;
                    }
                    else
                    {
                        bufferGraphics.DrawImage(
                            image,
                            new RectangleF(0, y, image.Width, image.Height),
                            new RectangleF(0, 0, image.PhysicalDimension.Width, image.PhysicalDimension.Height),
                            GraphicsUnit.Pixel);
                        y += image.Height;
                    }

                    //await WriteDebugImageInSegments(buffer, 16000, "stitch_buf_" + stitchBufIdx, fileSystem, outputDirectory);
                    stitchBufIdx++;
                }

                return buffer;
            }
        }

        private static async Task<IList<Bitmap>> SplitImageAtDistinctBreaks(ILogger logger, Bitmap inputImage, IFileSystem fileSystem, VirtualPath outputDir)
        {
            // Find lines which are completely different from the one before
            if (inputImage.PixelFormat != PixelFormat.Format24bppRgb)
            {
                throw new FormatException("Wrong bitmap format");
            }

            logger.Log("Detecting subimages...", LogLevel.Vrb);
            List<Tuple<int, int>> subImageBounds = new List<Tuple<int, int>>();
            int lastSubImageY = 0;
            List<Tuple<int, int>> subImages = new List<Tuple<int, int>>();
            byte[] previousLine = new byte[inputImage.Width * 3];
            BitmapData raster = inputImage.LockBits(new Rectangle(0, 0, inputImage.Width, inputImage.Height), ImageLockMode.ReadOnly, inputImage.PixelFormat);
            try
            {
                unsafe
                {
                    byte* rasterPtr = (byte*)raster.Scan0.ToPointer();
                    for (int line = 0; line < inputImage.Height; line++)
                    {
                        int lineByte = 0;
                        byte* linePtr = rasterPtr + (line * raster.Stride);
                        bool thisLineIsCompletelyDifferent = true;
                        int diff;
                        for (int x = 0; x < inputImage.Width; x++)
                        {
                            diff = 0;
                            byte colorByte = linePtr[lineByte];
                            diff += Math.Abs((int)colorByte - (int)previousLine[lineByte]);
                            previousLine[lineByte++] = colorByte;
                            colorByte = linePtr[lineByte];
                            diff += Math.Abs((int)colorByte - (int)previousLine[lineByte]);
                            previousLine[lineByte++] = colorByte;
                            colorByte = linePtr[lineByte];
                            diff += Math.Abs((int)colorByte - (int)previousLine[lineByte]);
                            previousLine[lineByte++] = colorByte;
                            thisLineIsCompletelyDifferent = thisLineIsCompletelyDifferent && diff > RGB_THRESH_FOR_DISTINCT_SUBPAGE;
                        }

                        if (thisLineIsCompletelyDifferent &&
                            line != 0 &&
                            (line - lastSubImageY) >= MIN_PAGE_HEIGHT_PX)
                        {
                            // New subimage
                            logger.Log("Found distinct subimage " + lastSubImageY + " -> " + line, LogLevel.Vrb);
                            subImageBounds.Add(new Tuple<int, int>(lastSubImageY, line));
                            lastSubImageY = line + 1;
                        }
                    }
                }
            }
            finally
            {
                inputImage.UnlockBits(raster);
            }

            if (lastSubImageY != inputImage.Height)
            {
                subImageBounds.Add(new Tuple<int, int>(lastSubImageY, inputImage.Height));
            }

            List<Bitmap> returnVal = new List<Bitmap>();
            int subImageIdx = 0;
            foreach (Tuple<int, int> subImageBound in subImageBounds)
            {
                if (subImageBound.Item2 > subImageBound.Item1)
                {
                    Bitmap subImage = inputImage.Clone(new Rectangle(0, subImageBound.Item1, inputImage.Width, subImageBound.Item2 - subImageBound.Item1), inputImage.PixelFormat);

                    // debug dump of subimages
                    //VirtualPath subImageFileName = outputDir.Combine(string.Format("sub_{0:D3}.jpg", subImageIdx));
                    //using (Stream debugStream = await fileSystem.OpenStreamAsync(subImageFileName, FileOpenMode.Create, FileAccessMode.Write))
                    //{
                    //    subImage.Save(debugStream, ImageFormat.Png);
                    //}

                    subImageIdx++;
                    returnVal.Add(subImage);
                }
            }

            return returnVal;
        }

        private static async Task<IList<Bitmap>> SplitSingleImageIntoPages(ILogger logger, Bitmap inputImage, IFileSystem fileSystem, VirtualPath outputDir)
        {
            List<Bitmap> returnVal = new List<Bitmap>();
            if (inputImage == null || inputImage.Height < MIN_VIABLE_PAGE_HEIGHT)
            {
                logger.Log("Input image is null or negligibly small; ignoring", LogLevel.Wrn);
                return returnVal;
            }

            int bufferWidth = inputImage.Width;
            int bufferTotalLines = inputImage.Height;

            float[] lineBlankMeasurement = new float[bufferTotalLines];
            float[] naturalPageSplitLines = new float[bufferTotalLines];
            float[] paginationPreference = new float[bufferTotalLines];

            // Find areas of low variation within the buffer
            float totalLineVariation = 0;
            float minLineVariation = 1000;
            logger.Log("Calculating image variation...", LogLevel.Vrb);
            int bytesPerPixel = 3;
            BitmapData raster = inputImage.LockBits(new Rectangle(0, 0, bufferWidth, bufferTotalLines), ImageLockMode.ReadOnly, inputImage.PixelFormat);
            try
            {
                unsafe
                {
                    byte* rasterPtr = (byte*)raster.Scan0.ToPointer();
                    for (int line = 0; line < bufferTotalLines; line++)
                    {
                        byte* linePtr = rasterPtr + (line * raster.Stride);
                        long avgR = 0;
                        long avgG = 0;
                        long avgB = 0;
                        for (int x = 0; x < bufferWidth; x++)
                        {
                            avgR += linePtr[(x * bytesPerPixel) + 0];
                            avgG += linePtr[(x * bytesPerPixel) + 1];
                            avgB += linePtr[(x * bytesPerPixel) + 2];
                        }

                        avgR /= bufferWidth;
                        avgG /= bufferWidth;
                        avgB /= bufferWidth;

                        long diff = 0;
                        for (int x = 0; x < bufferWidth; x++)
                        {
                            diff += Math.Abs((int)linePtr[(x * bytesPerPixel) + 0] - avgR);
                            diff += Math.Abs((int)linePtr[(x * bytesPerPixel) + 1] - avgG);
                            diff += Math.Abs((int)linePtr[(x * bytesPerPixel) + 2] - avgB);
                        }

                        float thisLineVariance = ((float)diff) / ((float)bufferWidth);

                        // 4 is about the threshold for jpeg noise on a solid field to indicate a blank line
                        // 100 is "there's definitely some stuff going on";
                        // The max variance is usually somewhere around 300
                        lineBlankMeasurement[line] = Math.Max(0.0f, 1.0f - (thisLineVariance / 200f));
                        totalLineVariation += lineBlankMeasurement[line];
                        minLineVariation = Math.Min(minLineVariation, lineBlankMeasurement[line]);
                    }
                }
            }
            finally
            {
                inputImage.UnlockBits(raster);
            }

            // Is this entire image just whitespace?
            float averagePageLineVariance = totalLineVariation / (float)bufferTotalLines;
            logger.Log("Sub page avg line variance is " + averagePageLineVariance, LogLevel.Vrb);
            if (averagePageLineVariance > BLANK_PAGE_TOTAL_VARIANCE_THRESH &&
                minLineVariation > BLANK_PAGE_SINGLE_LINE_VARIANCE_THRESH)
            {
                logger.Log("Input subimage is featureless; ignoring", LogLevel.Wrn);
                return returnVal;
            }

            // Detect leading and trailing whitespace. Use this to limit the size of the entire image (particularly so we don't end up with a lot of whitespace at the end of dynamically split pages)
            int usableImageStartLine = 0;
            for (; usableImageStartLine < bufferTotalLines && naturalPageSplitLines[usableImageStartLine] >= BLANK_LINE_THRESH; usableImageStartLine++) ;
            int usableImageEndLine = bufferTotalLines - 1;
            for (; usableImageEndLine > usableImageStartLine && naturalPageSplitLines[usableImageEndLine] >= BLANK_LINE_THRESH; usableImageEndLine--) ;
            if (usableImageEndLine - usableImageStartLine < MIN_VIABLE_PAGE_HEIGHT)
            {
                logger.Log("Input image (minus whitespace) is negligibly small; ignoring", LogLevel.Wrn);
                return returnVal;
            }

            usableImageStartLine = Math.Max(0, usableImageStartLine - DESIRED_PAGE_PADDING_PX);
            usableImageEndLine = Math.Min(bufferTotalLines - 1, usableImageEndLine + DESIRED_PAGE_PADDING_PX);

            logger.Log("Convolving...", LogLevel.Vrb);

            // Convert blank space detection results into an ideal page split gradient
            for (int line = 0; line < bufferTotalLines; line++)
            {
                int avgStartLine = Math.Max(0, line - DESIRED_PAGE_PADDING_PX);
                int avgEndLine = Math.Min(bufferTotalLines, line + DESIRED_PAGE_PADDING_PX);
                float sum = 0;
                for (int windowLine = avgStartLine; windowLine < avgEndLine; windowLine++)
                {
                    sum += lineBlankMeasurement[windowLine];
                }

                naturalPageSplitLines[line] = sum / (float)(avgEndLine - avgStartLine);
            }

            int cleanPageStartLine = usableImageStartLine;
            int cleanPageEndLine = 0;

            // From the line blank measurement, we can calculate "clean" pages where the regions are obviously padded with whitespace and we can crop freely
            logger.Log("Searching for clean page breaks...", LogLevel.Vrb);
            while (cleanPageStartLine < usableImageEndLine)
            {
                // Find start of page
                for (; cleanPageStartLine < usableImageEndLine && naturalPageSplitLines[cleanPageStartLine] >= BLANK_LINE_THRESH; cleanPageStartLine++) ;

                if (cleanPageStartLine >= usableImageEndLine)
                {
                    // Reached end of buffer and found only whitespace. So nothing left to do.
                    break;
                }

                // Find end of page
                cleanPageEndLine = cleanPageStartLine;
                int cleanPageEndLineHyp = cleanPageEndLine;
                while (true)
                {
                    for (; cleanPageEndLineHyp < usableImageEndLine && naturalPageSplitLines[cleanPageEndLineHyp] < BLANK_LINE_THRESH; cleanPageEndLineHyp++) ;
                    if (cleanPageEndLine == cleanPageStartLine ||
                        (cleanPageEndLine - cleanPageStartLine) < MIN_PAGE_HEIGHT_PX ||
                        cleanPageEndLineHyp <= cleanPageStartLine + PREFERRED_PAGE_HEIGHT_PX)
                    {
                        // If this is the first hypothesis, or the entire hypothesized region will still fit on a single page, continue searching
                        cleanPageEndLine = cleanPageEndLineHyp;

                        // Unless our hypothesis search has reached the end of the buffer
                        if (cleanPageEndLineHyp >= usableImageEndLine)
                        {
                            break;
                        }

                        for (; cleanPageEndLineHyp < usableImageEndLine && naturalPageSplitLines[cleanPageEndLineHyp] >= BLANK_LINE_THRESH; cleanPageEndLineHyp++) ;
                    }
                    else
                    {
                        // Hypothesized clean page is equal to or larger than desired size. Break the loop
                        break;
                    }
                }

                // Back up and remove whitespace from the hypothesized page
                // Make sure we remember where the next clean page starts when we do this
                int startOfNextCleanPage = cleanPageEndLine;
                for (; cleanPageEndLine > cleanPageStartLine && naturalPageSplitLines[cleanPageEndLine] >= BLANK_LINE_THRESH; cleanPageEndLine--) ;
                if (cleanPageEndLine - cleanPageStartLine < MIN_VIABLE_PAGE_HEIGHT)
                {
                    logger.Log("Input image (minus whitespace) is negligibly small; ignoring", LogLevel.Wrn);
                    cleanPageStartLine = cleanPageEndLine;
                }
                else
                {
                    cleanPageEndLine = Math.Min(usableImageEndLine, cleanPageEndLine + DESIRED_PAGE_PADDING_PX);
                }

                int cleanPageHeight = cleanPageEndLine - cleanPageStartLine;
                logger.Log("Clean crop found from line " + cleanPageStartLine + " -> " + cleanPageEndLine + " (height " + cleanPageHeight + ")", LogLevel.Vrb);
                if (cleanPageHeight <= 1)
                {
                    logger.Log("Empty page detected at line " + cleanPageStartLine, LogLevel.Wrn);
                }
                else if (cleanPageHeight <= MAX_PAGE_HEIGHT_PX)
                {
                    // If the clean crop is within the maximum page height, go ahead and just use it
                    // But first check to make sure it's not featureless
                    totalLineVariation = 0;
                    minLineVariation = 1000;
                    for (int idx = cleanPageStartLine; idx < cleanPageEndLine; idx++)
                    {
                        totalLineVariation += lineBlankMeasurement[idx];
                        minLineVariation = Math.Min(minLineVariation, lineBlankMeasurement[idx]);
                    }

                    averagePageLineVariance = totalLineVariation / (float)cleanPageHeight;
                    logger.Log("Clean crop avg line variance is " + averagePageLineVariance, LogLevel.Vrb);
                    if (averagePageLineVariance > BLANK_PAGE_TOTAL_VARIANCE_THRESH &&
                        minLineVariation > BLANK_PAGE_SINGLE_LINE_VARIANCE_THRESH)
                    {
                        logger.Log("Clean crop image is featureless; ignoring", LogLevel.Wrn);
                    }
                    else
                    {
                        logger.Log("Clean crop fits within one page; copying directly...", LogLevel.Vrb);
                        Bitmap cleanCrop = inputImage.Clone(new Rectangle(0, cleanPageStartLine, inputImage.Width, cleanPageHeight), inputImage.PixelFormat);
                        returnVal.Add(cleanCrop);
                    }
                }
                else
                {
                    logger.Log("Clean crop spans multiple pages; subdividing...", LogLevel.Vrb);
                    int subdividedPageStart = cleanPageStartLine;
                    while (subdividedPageStart < cleanPageEndLine)
                    {
                        // Does the remainder fit within max page height?
                        if (cleanPageEndLine - subdividedPageStart <= MAX_PAGE_HEIGHT_PX)
                        {
                            // Check if it's featureless
                            totalLineVariation = 0;
                            minLineVariation = 1000;
                            for (int idx = subdividedPageStart; idx < cleanPageEndLine; idx++)
                            {
                                totalLineVariation += lineBlankMeasurement[idx];
                                minLineVariation = Math.Min(minLineVariation, lineBlankMeasurement[idx]);
                            }

                            averagePageLineVariance = totalLineVariation / (float)(cleanPageEndLine - subdividedPageStart);
                            logger.Log("Subdivided page avg line variance is " + averagePageLineVariance, LogLevel.Vrb);
                            if (averagePageLineVariance > BLANK_PAGE_TOTAL_VARIANCE_THRESH &&
                                minLineVariation > BLANK_PAGE_SINGLE_LINE_VARIANCE_THRESH)
                            {
                                logger.Log("Subdivided image is featureless; ignoring", LogLevel.Wrn);
                            }
                            else
                            {
                                Bitmap remainder = inputImage.Clone(new Rectangle(0, subdividedPageStart, bufferWidth, (cleanPageEndLine - subdividedPageStart)), inputImage.PixelFormat);
                                returnVal.Add(remainder);
                            }

                            break;
                        }

                        // Otherwise, we need to subdivide it even more based on the variation map we already calculated, plus a pagination preference map
                        ApplyPaginationWindow(
                            paginationPreference,
                            cleanPageStartLine,
                            cleanPageEndLine,
                           -100.0f,
                            -100.0f);
                        ApplyPaginationWindow(
                            paginationPreference,
                            subdividedPageStart + MIN_PAGE_HEIGHT_PX,
                            subdividedPageStart + PREFERRED_PAGE_HEIGHT_PX,
                            0.0f,
                            1.0f);
                        ApplyPaginationWindow(
                            paginationPreference,
                            subdividedPageStart + PREFERRED_PAGE_HEIGHT_PX,
                            subdividedPageStart + MAX_PAGE_HEIGHT_PX,
                            1.0f,
                            -1.0f);

                        // Now find the ideal page cutoff
                        float bestCutoffScore = 0;
                        int pageCutoffPoint = subdividedPageStart;
                        for (int line = subdividedPageStart; line < cleanPageEndLine && line < subdividedPageStart + MAX_PAGE_HEIGHT_PX; line++)
                        {
                            float thisScore = (2.0f * naturalPageSplitLines[line]) + (1.0f * paginationPreference[line]);
                            if (thisScore > bestCutoffScore)
                            {
                                bestCutoffScore = thisScore;
                                pageCutoffPoint = line;
                            }
                        }

                        if (pageCutoffPoint == subdividedPageStart)
                        {
                            // ?
                            logger.Log("No ideal page split detected; regressing to max page height", LogLevel.Wrn);
                            pageCutoffPoint = Math.Min(cleanPageEndLine, subdividedPageStart + MAX_PAGE_HEIGHT_PX);
                        }

                        //Bitmap debugBitmap = new Bitmap(bufferWidth * 2, cleanPageHeight, PixelFormat.Format32bppArgb);
                        //using (Graphics debugGraphics = Graphics.FromImage(debugBitmap))
                        //{
                        //    Bitmap cleanCrop = inputImage.Clone(new Rectangle(0, cleanPageStartLine, inputImage.Width, cleanPageHeight), inputImage.PixelFormat);
                        //    debugGraphics.DrawImage(cleanCrop, new Point(bufferWidth, 0));
                        //    for (int line = 0; line < cleanPageHeight; line++)
                        //    {
                        //        int bufferLine = line + cleanPageStartLine;
                        //        float metric = ((2.0f * naturalPageSplitLines[bufferLine]) + (1.0f * paginationPreference[bufferLine])) / 3.0f;
                        //        int debugColor = Math.Max(0, Math.Min(255, (int)(metric * 255)));
                        //        debugGraphics.DrawLine(new Pen(new SolidBrush(Color.FromArgb(255, debugColor, debugColor, debugColor))), 0, line, bufferWidth, line);
                        //    }

                        //    debugGraphics.DrawLine(new Pen(new SolidBrush(Color.FromArgb(255, 255, 0, 0))), 0, subdividedPageStart - cleanPageStartLine, bufferWidth, subdividedPageStart - cleanPageStartLine);
                        //    debugGraphics.DrawLine(new Pen(new SolidBrush(Color.FromArgb(255, 255, 0, 0))), 0, Math.Min(cleanPageEndLine, subdividedPageStart + maxPageHeight) - cleanPageStartLine, bufferWidth, Math.Min(cleanPageEndLine, subdividedPageStart + maxPageHeight) - cleanPageStartLine);
                        //    debugGraphics.DrawLine(new Pen(new SolidBrush(Color.FromArgb(255, 0, 255, 0))), 0, pageCutoffPoint - cleanPageStartLine, bufferWidth, pageCutoffPoint - cleanPageStartLine);

                        //    using (Stream debugStream = await fileSystem.OpenStreamAsync(outputDir.Combine("debug.png"), FileOpenMode.Create, FileAccessMode.Write))
                        //    {
                        //        debugBitmap.Save(debugStream, ImageFormat.Png);
                        //    }
                        //}

                        logger.Log("Subdividing clean page at line " + pageCutoffPoint + " (height " + (pageCutoffPoint - subdividedPageStart) + ")", LogLevel.Vrb);
                        // Check if it's featureless
                        totalLineVariation = 0;
                        minLineVariation = 1000;
                        for (int idx = subdividedPageStart; idx < pageCutoffPoint; idx++)
                        {
                            totalLineVariation += lineBlankMeasurement[idx];
                            minLineVariation = Math.Min(minLineVariation, lineBlankMeasurement[idx]);
                        }

                        averagePageLineVariance = totalLineVariation / (float)(pageCutoffPoint - subdividedPageStart);
                        if (averagePageLineVariance > BLANK_PAGE_TOTAL_VARIANCE_THRESH &&
                             minLineVariation > BLANK_PAGE_SINGLE_LINE_VARIANCE_THRESH)
                        {
                            logger.Log("Subdivided image is featureless; ignoring", LogLevel.Wrn);
                        }
                        else
                        {
                            Bitmap newBitmap = inputImage.Clone(new Rectangle(0, subdividedPageStart, bufferWidth, (pageCutoffPoint - subdividedPageStart)), inputImage.PixelFormat);
                            returnVal.Add(newBitmap);
                        }

                        // Apply a small overlap when starting the next page, to prevent losing important details along the line break
                        subdividedPageStart = Math.Max(subdividedPageStart + 1, pageCutoffPoint - SUBDIVIDED_PAGE_OVERLAP);
                    }
                }

                cleanPageStartLine = startOfNextCleanPage;
            }

            return returnVal;
        }

        private static async Task WriteDebugImageInSegments(Bitmap image, int maxHeight, string fileNamePrefix, IFileSystem fileSystem, VirtualPath outputDirectory)
        {
            int y = 0;
            int outputImageIdx = 0;
            while (y < image.Height)
            {
                int nextImgHeight = Math.Min(maxHeight, image.Height - y);
                using (Bitmap subImage = image.Clone(new Rectangle(0, y, image.Width, nextImgHeight), image.PixelFormat))
                {
                    using (Stream debugStream = await fileSystem.OpenStreamAsync(outputDirectory.Combine(string.Format("{0}_{1:D1}.jpg", fileNamePrefix, outputImageIdx)), FileOpenMode.Create, FileAccessMode.Write))
                    {
                        subImage.Save(debugStream, ImageFormat.Jpeg);
                    }

                    y += nextImgHeight;
                    outputImageIdx++;
                }
            }
        }

        private static void ApplyPaginationWindow(float[] buffer, int startLine, int endLine, float startValue, float endValue)
        {
            startLine = Math.Max(0, startLine);
            endLine = Math.Min(buffer.Length, endLine);
            int numLines = endLine - startLine;
            if (numLines <= 0)
            {
                return;
            }

            float paginationIdx = startValue;
            float paginationInc = (endValue - startValue) / (float)numLines;
            for (int line = startLine; line < endLine; line++)
            {
                buffer[line] = paginationIdx;
                paginationIdx += paginationInc;
            }
        }
    }
}
