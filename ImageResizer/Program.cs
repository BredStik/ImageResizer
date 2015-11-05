using Akka.Actor;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using ImageResizer.Actors;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ImageResizer
{
    class Program
    {
        public static ActorSystem ResizerActors;

        static void Main(string[] args)
        {
            ResizerActors = ActorSystem.Create("ResizerActors");
            var commanderActor = ResizerActors.ActorOf(Props.Create(() => new CommanderActor()));
            
           var imagePath = args[0];

            var profileDictionary = new Dictionary<string, int>
            {
                { "Icon-40.png", 40 },
                { "Icon-40-2x.png", 80 },
                { "Icon-60.png", 60 },
                { "Icon-60-2x.png", 60 },
                {"Icon-72.png",72},
                {"Icon-72-2x.png",144},
                {"Icon-76.png",76},
                {"Icon-76-2x.png", 152},
                {"Icon-Small-50.png", 50},
                { "Icon-Small-50-2x.png", 100},
                { "Icon-Small.png", 29},
                { "Icon-Small-2x.png", 58},
                { "Icon.png", 57},
                { "Icon-2x.png", 114},
                { "iTunesArtwork.png", 512},
                { "iTunesArtwork-2x.png", 1024},
                { "Icon-16.png", 16},
                { "Icon-24.png", 24},
                { "Icon-32.png", 32},
                { "Icon-64.png", 64},
                { "Icon-120.png", 120},
                { "Icon-152.png", 152},
                { "Icon-Small-40.png", 40},
                { "Icon-Small-40-2x.png", 80},
                { "Icon-60-3x.png", 180},
                { "Icon-Small-3x.png", 87},
                { "Icon-Small-40-3x.png", 120}
            };
            //var allTasks = profileDictionary.Keys.Select(key =>
            //{
            //    return Task<Tuple<string, Stream>>.Factory.StartNew(() => Save(new Bitmap(imagePath), profileDictionary[key], profileDictionary[key], 1, key));
            //});

            //Task.WaitAll(allTasks.ToArray());

            //var results = allTasks.Select(x => x.Result).ToArray();

            //CreateToMemoryStream(results, "test.zip");

            byte[] originalImage;
            using (var fs = File.OpenRead(imagePath))
            {
                originalImage = new byte[fs.Length];
                fs.Read(originalImage, 0, (int)fs.Length);
            }

            //coordinatorActor.Tell(new CoordinatorActor.ResizeImage(originalImage, profileDictionary, "test.zip"));
            var command = Console.ReadLine();
            while(command == "")
            {
                //Task.Run(() => {
                //    Parallel.For(0, 10, index => {
                //        //var coordinatorActor = ResizerActors.ActorOf(Props.Create(() => new CoordinatorActor()));
                //        commanderActor.Tell(new CoordinatorActor.ResizeImage(originalImage, profileDictionary, string.Format("{0}.zip", Guid.NewGuid())));
                //    });
                //});
                commanderActor.Tell(new CoordinatorActor.ResizeImage(originalImage, profileDictionary, string.Format("{0}.zip", Guid.NewGuid())));
                
                command = Console.ReadLine();
            }
            
            ResizerActors.Shutdown();
            ResizerActors.Dispose();
        }

        public static Tuple<string,Stream> Save(Bitmap image, int maxWidth, int maxHeight, int quality, string filePath)
        {
            // Get the image's original width and height
            int originalWidth = image.Width;
            int originalHeight = image.Height;


            int newWidth;
            int newHeight;
            float ratio;

            if(originalWidth > originalHeight)
            {
                newWidth = maxWidth;
                ratio = (float)maxWidth / (float)originalWidth;
                newHeight = (int)(originalHeight * ratio);
            }
            else if(originalHeight > originalWidth)
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

        // Compresses the supplied memory stream, naming it as zipEntryName, into a zip,
        // which is returned as a memory stream or a byte array.
        //
        public static void CreateToMemoryStream(IEnumerable<Tuple<string, Stream>> entries, string zipName)
        {
            MemoryStream outputMemStream = new MemoryStream();
            ZipOutputStream zipStream = new ZipOutputStream(outputMemStream);

            zipStream.SetLevel(3); //0-9, 9 being the highest level of compression

            foreach (var entry in entries)
            {
                ZipEntry newEntry = new ZipEntry(entry.Item1);
                newEntry.DateTime = DateTime.Now;

                zipStream.PutNextEntry(newEntry);

                StreamUtils.Copy(entry.Item2, zipStream, new byte[4096]);
                zipStream.CloseEntry();
            }           

            zipStream.IsStreamOwner = false;    // False stops the Close also Closing the underlying stream.
            zipStream.Close();          // Must finish the ZipOutputStream before using outputMemStream.

            outputMemStream.Position = 0;
            File.WriteAllBytes(zipName, outputMemStream.ToArray());

            //// Alternative outputs:
            //// ToArray is the cleaner and easiest to use correctly with the penalty of duplicating allocated memory.
            //byte[] byteArrayOut = outputMemStream.ToArray();

            //// GetBuffer returns a raw buffer raw and so you need to account for the true length yourself.
            //byte[] byteArrayOut = outputMemStream.GetBuffer();
            //long len = outputMemStream.Length;
        }
    }
}
