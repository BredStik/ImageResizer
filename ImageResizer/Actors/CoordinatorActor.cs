using Akka.Actor;
using Akka.Routing;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ImageResizer.Actors
{
    public class CoordinatorActor: ReceiveActor
    {
        public class ResizeImage
        {
            public byte[] OriginalImage { get; private set; }
            public Dictionary<string, int> Profiles { get; private set; }
            public string DestinationFile { get; set; }
            public ResizeImage(byte[] originalImage, Dictionary<string, int> profiles, string destinationFile)
            {
                OriginalImage = originalImage;
                Profiles = profiles;
                DestinationFile = destinationFile;
            }
        }

        public class ImageResized
        {
            public Stream Image { get; private set; }
            public string ProfileName { get; private set; }
            public Guid SagaId { get; private set; }
            public ImageResized(Stream image, string profileName, Guid sagaId)
            {
                Image = image;
                ProfileName = profileName;
                SagaId = sagaId;
            }
        }

        public class ZipIt { }

        public class CoordinatorDone { }

        private IActorRef _resizeWorker;
        
        protected override void PreStart()
        {
            _resizeWorker = Context.ActorOf(Props.Create(() => new ResizeWorkerActor())
                .WithRouter(new RoundRobinPool(8)));
            base.PreStart();
        }

        private IDictionary<Guid, int> _sagas = new Dictionary<Guid, int>();
        private IDictionary<string, Stream> _resizedImages = new Dictionary<string, Stream>();
        private string _destinationFile;

        private void Ready()
        {
            Receive<ResizeImage>(message => {

                var sagaId = Guid.NewGuid();
                _sagas.Add(sagaId, message.Profiles.Count);
                _destinationFile = message.DestinationFile;
                foreach (var profile in message.Profiles)
                {
                    _resizeWorker.Tell(new ResizeWorkerActor.ResizeImage(new MemoryStream(message.OriginalImage), profile.Value, profile.Key, sagaId));
                }

                Become(Working);
            });
        }

        private void Working()
        {
            Receive<ImageResized>(message => {
                //Console.WriteLine("Got resized image for {0}", message.ProfileName);
                _resizedImages.Add(message.ProfileName, message.Image);
                _sagas[message.SagaId] -= 1;

                var toDelete = new List<Guid>();

                foreach (var saga in _sagas)
                {
                    //Console.WriteLine("{0} images left in job {1}", saga.Value, saga.Key);
                    if (saga.Value == 0)
                    {
                        toDelete.Add(saga.Key);
                    }
                }

                foreach (var item in toDelete)
                {
                    _sagas.Remove(item);
                }

                if (_sagas.Count == 0)
                {
                    Become(ReadyToZip);
                    //all images processed, save to zip
                    Self.Tell(new ZipIt());
                }
            });

        }

        private void ReadyToZip()
        {
            Receive<ZipIt>(message => {
                CreateToMemoryStream(_resizedImages.Select(img => new Tuple<string, Stream>(img.Key, img.Value)), _destinationFile);
                Self.Tell(PoisonPill.Instance);
            });
        }

        public CoordinatorActor()
        {
            Ready();
        }

        private static void CreateToMemoryStream(IEnumerable<Tuple<string, Stream>> entries, string zipName)
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
