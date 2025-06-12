
namespace MediaControlTests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using MediaControl.Winamp;
    using Durandal.Common.Logger;
    using Durandal.Common.NLP.Alignment;
    using System.IO;
    using Durandal.Common.NLP.Language.English;
    using System.Collections.Generic;
    using Durandal.MediaProtocol;
    using Durandal.Common.File;
    using Durandal.Common.NLP;
    using Durandal.Common.Tasks;

    [TestClass]
    [DeploymentItem("winamp_library.dat")]
    [DeploymentItem("cmu-pronounce-ipa.dict")]
    [DeploymentItem("pron.cache")]
    public class WinampTests
    {
        private static InMemoryFileSystem _fakeCacheFilesystem;
        private static DirectoryInfo _fakeMediaDirectory;
        private static WinampLibrary _library;
        private static ILogger _logger;
        private static IPronouncer _pronouncer;
        private static EditDistancePronunciation _editDistance;

        [ClassInitialize]
        public static void InitializeTests(TestContext context)
        {
            _logger = new ConsoleLogger("TestMain", LogLevel.All);
            _fakeMediaDirectory = new DirectoryInfo("N:\\");

            _fakeCacheFilesystem = new InMemoryFileSystem();
            _fakeCacheFilesystem.AddFile(new VirtualPath("winamp_library.dat"), File.ReadAllBytes("winamp_library.dat"));
            _fakeCacheFilesystem.AddFile(new VirtualPath("cmu-pronounce-ipa.dict"), File.ReadAllBytes("cmu-pronounce-ipa.dict"));
            _fakeCacheFilesystem.AddFile(new VirtualPath("pron.cache"), File.ReadAllBytes("pron.cache"));

            _pronouncer = EnglishPronouncer.Create(new VirtualPath("cmu-pronounce-ipa.dict"), new VirtualPath("pron.cache"), _logger.Clone("Pronouncer"), _fakeCacheFilesystem).Await();
            _editDistance = new EditDistancePronunciation(_pronouncer, new EnglishWholeWordBreaker(), "en-us");
            _library = new WinampLibrary(NullLogger.Singleton, _editDistance.Calculate);
            _library.Initialize(_fakeMediaDirectory, _fakeCacheFilesystem);
        }

        [TestMethod]
        public void TestSearchEmptyQuery()
        {
            IList<WinampLibraryEntry> results = _library.Query(new EnqueueMediaCommand());
            Assert.IsNotNull(results);
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void TestSearchNullQuery()
        {
            IList<WinampLibraryEntry> results = _library.Query(null);
            Assert.IsNotNull(results);
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void TestSearchArtistBasic()
        {
            IList<WinampLibraryEntry> results = _library.Query(new EnqueueMediaCommand()
            {
                Artist = "Tally hall"
            });
            
            Assert.AreEqual(14, results.Count);
            foreach (WinampLibraryEntry entry in results)
            {
                Assert.AreEqual("Tally Hall", entry.AlbumArtist);
            }
        }

        [TestMethod]
        public void TestSearchArtistApproximate()
        {
            IList<WinampLibraryEntry> results = _library.Query(new EnqueueMediaCommand()
            {
                Artist = "Mindy Gladhill"
            });
            
            Assert.AreEqual(52, results.Count);
            foreach (WinampLibraryEntry entry in results)
            {
                Assert.AreEqual("Mindy Gledhill", entry.AlbumArtist);
            }
        }

        [TestMethod]
        public void TestSearchArtistLargeResultSet()
        {
            IList<WinampLibraryEntry> results = _library.Query(new EnqueueMediaCommand()
            {
                Artist = "Mogwai"
            });

            Assert.AreEqual(135, results.Count);
            foreach (WinampLibraryEntry entry in results)
            {
                Assert.AreEqual("Mogwai", entry.AlbumArtist);
            }
        }

        [TestMethod]
        public void TestSearchArtistNotFound()
        {
            IList<WinampLibraryEntry> results = _library.Query(new EnqueueMediaCommand()
            {
                Artist = "Ariana Grande"
            });

            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void TestSearchAlbumBasic()
        {
            IList<WinampLibraryEntry> results = _library.Query(new EnqueueMediaCommand()
            {
                Album = "random access memories"
            });

            Assert.AreEqual(13, results.Count);
            foreach (WinampLibraryEntry entry in results)
            {
                Assert.AreEqual("Random Access Memories", entry.Album);
            }
        }

        [TestMethod]
        public void TestSearchTitleAndArtist()
        {
            IList<WinampLibraryEntry> results = _library.Query(new EnqueueMediaCommand()
            {
                Title = "get lucky",
                Artist = "daft punk"
            });

            Assert.AreEqual(1, results.Count);
            foreach (WinampLibraryEntry entry in results)
            {
                Assert.AreEqual("Random Access Memories", entry.Album);
                Assert.AreEqual("Daft Punk ft. Pharell", entry.Artist);
            }
        }

        [TestMethod]
        public void TestSearchAlbumAndTitle()
        {
            IList<WinampLibraryEntry> results = _library.Query(new EnqueueMediaCommand()
            {
                Title = "funny bunny",
                Album = "fool on the planet"
            });

            Assert.AreEqual(1, results.Count);
            foreach (WinampLibraryEntry entry in results)
            {
                Assert.AreEqual("Fool On The Planet", entry.Album);
            }
        }

        [TestMethod]
        public void TestSearchAlbumAndArtist()
        {
            IList<WinampLibraryEntry> results = _library.Query(new EnqueueMediaCommand()
            {
                Artist = "seeds of kindness",
                Album = "building bridges"
            });

            Assert.AreEqual(19, results.Count);
            foreach (WinampLibraryEntry entry in results)
            {
                Assert.AreEqual("Building Bridges", entry.Album);
                Assert.AreEqual("Seeds of Kindness", entry.AlbumArtist);
            }
        }

        [TestMethod]
        public void TestSearchArtistWithSubquery()
        {
            IList<WinampLibraryEntry> results = _library.Query(new EnqueueMediaCommand()
            {
                Artist = "the smashing pumpkins",
                Subquery = "acoustic"
            });

            Assert.AreEqual(10, results.Count);
            foreach (WinampLibraryEntry entry in results)
            {
                Assert.IsTrue(entry.Title.ToLowerInvariant().Contains("acoustic"));
                Assert.AreEqual("The Smashing Pumpkins", entry.AlbumArtist);
            }
        }

        [TestMethod]
        public void TestSearchArtistNormalization()
        {
            IList<WinampLibraryEntry> results = _library.Query(new EnqueueMediaCommand()
            {
                Artist = "cure",
            });

            Assert.AreEqual(3, results.Count);
            foreach (WinampLibraryEntry entry in results)
            {
                Assert.AreEqual("The Cure", entry.AlbumArtist);
            }
        }

        [TestMethod]
        public void TestSearchSubqueryFiltersOutEverything()
        {
            IList<WinampLibraryEntry> results = _library.Query(new EnqueueMediaCommand()
            {
                Artist = "the smashing pumpkins",
                Subquery = "this is a string that doesn't exist"
            });

            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void TestSearchTitle()
        {
            IList<WinampLibraryEntry> results = _library.Query(new EnqueueMediaCommand()
            {
                Title = "mashups like this"
            });

            Assert.AreEqual(1, results.Count);
            foreach (WinampLibraryEntry entry in results)
            {
                Assert.AreEqual("Triple-Q", entry.Artist);
            }
        }

        [TestMethod]
        public void TestSearchKeyword()
        {
            IList<WinampLibraryEntry> results = _library.Query(new EnqueueMediaCommand()
            {
                SearchTerm = "js16"
            });

            Assert.AreEqual(1, results.Count);
            foreach (WinampLibraryEntry entry in results)
            {
                Assert.AreEqual("JS16", entry.Artist);
            }
        }

        [TestMethod]
        public void TestSearchQueryAndSubquery()
        {
            IList<WinampLibraryEntry> results = _library.Query(new EnqueueMediaCommand()
            {
                SearchTerm = "the pillows",
                Subquery = "live"
            });

            Assert.AreEqual(49, results.Count);
            foreach (WinampLibraryEntry entry in results)
            {
                Assert.AreEqual("the pillows", entry.Artist);
                Assert.IsTrue(entry.Album.Contains("Live") || entry.Title.Contains("Live"));
            }
        }
    }
}
