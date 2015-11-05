using Akka.Actor;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ImageResizer.Actors
{
    public class ResizeWorkerActor: ReceiveActor
    {
        public class ResizeImage
        {
            public Stream OriginalImage { get; private set; }
            public int Size { get; private set; }
            public string DestinationFile { get; set; }
            public Guid SagaId { get; private set; }
            public ResizeImage(Stream originalImage, int size, string destinationFile, Guid sagaId)
            {
                OriginalImage = originalImage;
                Size = size;
                DestinationFile = destinationFile;
                SagaId = sagaId;
            }
        }

        public ResizeWorkerActor()
        {
            Receive<ResizeImage>(message => {
                //Console.WriteLine("Resizing image on thread {0} according to profile {1}", Thread.CurrentThread.ManagedThreadId, message.DestinationFile);
                var resized = Resize(new Bitmap(message.OriginalImage), message.Size, message.Size, 1, message.DestinationFile);

                Sender.Tell(new CoordinatorActor.ImageResized(resized.Item2, message.DestinationFile, message.SagaId));
            });
        }

        private Tuple<string, Stream> Resize(Bitmap image, int maxWidth, int maxHeight, int quality, string filePath)
        {
            // Get the image's original width and height
            int originalWidth = image.Width;
            int originalHeight = image.Height;


            int newWidth;
            int newHeight;
            float ratio;

            if (originalWidth > originalHeight)
            {
                newWidth = maxWidth;
                ratio = (float)maxWidth / (float)originalWidth;
                newHeight = (int)(originalHeight * ratio);
            }
            else if (originalHeight > originalWidth)
            {
                newHeight = maxHeight;
                ratio = (float)maxHeight / (float)originalHeight;
                newWidth = (int)(originalWidth * ratio);
            }
            else
            {
                newHeight = maxHeight;
                newWidth = maxWidth;
            }

            //// To preserve the aspect ratio
            //float ratioX = (float)maxWidth / (float)originalWidth;
            //float ratioY = (float)maxHeight / (float)originalHeight;
            //float ratio = Math.Min(ratioX, ratioY);

            //// New width and height based on aspect ratio
            //int newWidth = (int)(originalWidth * ratio);
            //int newHeight = (int)(originalHeight * ratio);

            // Convert other formats (including CMYK) to RGB.
            Bitmap newImage = new Bitmap(newWidth, newHeight, PixelFormat.Format24bppRgb);

            // Draws the image in the specified size with quality mode set to HighQuality
            using (Graphics graphics = Graphics.FromImage(newImage))
            {
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.DrawImage(image, 0, 0, newWidth, newHeight);
            }

            // Get an ImageCodecInfo object that represents the JPEG codec.
            ImageCodecInfo imageCodecInfo = GetEncoderInfo(ImageFormat.Png);

            // Create an Encoder object for the Quality parameter.
            System.Drawing.Imaging.Encoder encoder = System.Drawing.Imaging.Encoder.Quality;

            // Create an EncoderParameters object. 
            EncoderParameters encoderParameters = new EncoderParameters(1);

            // Save the image as a JPEG file with quality level.
            EncoderParameter encoderParameter = new EncoderParameter(encoder, quality);
            encoderParameters.Param[0] = encoderParameter;
            var ms = new MemoryStream();
            newImage.Save(ms, imageCodecInfo, encoderParameters);

            ms.Position = 0;

            return new Tuple<string, Stream>(filePath, ms);
        }

        /// <summary>
        /// Method to get encoder infor for given image format.
        /// </summary>
        /// <param name="format">Image format</param>
        /// <returns>image codec info.</returns>
        private static ImageCodecInfo GetEncoderInfo(ImageFormat format)
        {
            return ImageCodecInfo.GetImageDecoders().SingleOrDefault(c => c.FormatID == format.Guid);
        }
    }
}
