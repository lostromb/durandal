using Durandal.MediaProtocol;
using Durandal.Common.Logger;
using Durandal.Common.NLP.Alignment;
using MediaControl.Winamp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Durandal.Common.File;
using Durandal.Common.NLP;
using Durandal.Common.Tasks;

namespace MediaControl
{
    public class WinampController
    {
        private readonly WinampLibrary _musicLibrary;
        private readonly NLPTools _nlTools;
        private readonly EditDistancePronunciation _editDist;
        private readonly ILogger _logger;
        private readonly IFileSystem _libraryCacheFilesystem;

        public WinampController(ILogger logger, DirectoryInfo musicLibrary, DirectoryInfo cacheDirectory, NLPTools nlTools)
        {
            _logger = logger;
            _nlTools = nlTools;
            _editDist = new EditDistancePronunciation(_nlTools.Pronouncer, _nlTools.WordBreaker, "en-us");

            _libraryCacheFilesystem = new WindowsFileSystem(_logger.Clone("CacheFileManager"), cacheDirectory.FullName);

            _musicLibrary = new WinampLibrary(_logger.Clone("WinampLibrary"), _editDist.Calculate);
            _musicLibrary.Initialize(musicLibrary, _libraryCacheFilesystem);
        }

        public void UpdateLibrary()
        {
            _musicLibrary.UpdateLibrary();
        }

        public async Task<MediaControlResponse> Process(MediaControlRequest request)
        {
            MediaControlResponse response = new MediaControlResponse();
            response.Results = new List<MediaResult>();

            // Determine if processing this batch should start a new instance of winamp if not running
            bool shouldStartWinamp = false;
            foreach (MediaCommand command in request.Commands)
            {
                if (command is EnqueueMediaCommand)
                {
                    shouldStartWinamp = true;
                }
            }

            WinampInstance winamp = await WinampInstance.Get(shouldStartWinamp, _logger.Clone("Winamp"));
            
            // Process all commands serially
            try
            {
                foreach (MediaCommand command in request.Commands)
                {
                    MediaResult thisResult = await ProcessCommand(command, winamp);
                    response.Results.Add(thisResult);
                    //await Task.Delay(200);
                }
            }
            catch (Exception e)
            {
                // If a single exception happens, mark all the remaining unprocessed commands as failures
                while (response.Results.Count < request.Commands.Count)
                {
                    response.Results.Add(new FailureResult(e.Message));
                }
            }

            return response;
        }

        private async Task<MediaResult> ProcessCommand(MediaCommand command, WinampInstance winamp)
        {
            if (winamp == null)
            {
                return new NotExecutedResult("No Winamp process");
            }

            if (command is ClearPlaylistMediaCommand)
            {
                winamp.ClearPlaylist();
                return new SuccessResult();
            }
            else if (command is EnqueueMediaCommand)
            {
                EnqueueMediaCommand castCommand = (EnqueueMediaCommand)command;
                return EnqueueMedia(castCommand, winamp);
            }
            else if (command is PlayResumeMediaCommand)
            {
                winamp.Play();
                return new SuccessResult();
            }
            else if (command is PauseMediaCommand)
            {
                winamp.Pause();
                return new SuccessResult();
            }
            else if (command is SetRepeatMediaCommand)
            {
                SetRepeatMediaCommand castCommand = (SetRepeatMediaCommand)command;
                winamp.Repeat = castCommand.Mode;
                return new SuccessResult();
            }
            else if (command is SetShuffleMediaCommand)
            {
                SetShuffleMediaCommand castCommand = (SetShuffleMediaCommand)command;
                winamp.Shuffle = castCommand.EnableShuffle;
                return new SuccessResult();
            }
            else if (command is StopMediaCommand)
            {
                winamp.Stop();
                return new SuccessResult();
            }
            else if (command is VolumeControlMediaCommand)
            {
                VolumeControlMediaCommand castCommand = (VolumeControlMediaCommand)command;
                if (castCommand.ChangeType == ValueChangeType.Absolute)
                {
                    winamp.Volume = castCommand.Value;
                }
                else if (castCommand.ChangeType == ValueChangeType.RelativeAdd)
                {
                    winamp.Volume = winamp.Volume + castCommand.Value;
                }
                else if (castCommand.ChangeType == ValueChangeType.RelativeMultiply)
                {
                    winamp.Volume = winamp.Volume * castCommand.Value;
                }
                else
                {
                    return new NotImplementedResult();
                }

                return new SuccessResult();
            }
            else
            {
                await DurandalTaskExtensions.NoOpTask;
                return new NotImplementedResult();
            }
        }

        private MediaResult EnqueueMedia(EnqueueMediaCommand command, WinampInstance winamp)
        {
            IList<WinampLibraryEntry> results = _musicLibrary.Query(command);
            
            if (results == null || results.Count == 0)
            {
                return new FailureResult("No results found");
            }

            List<FileInfo> files = new List<FileInfo>();
            foreach (WinampLibraryEntry track in results)
            {
                files.Add(new FileInfo(track.FilePath));
            }

            winamp.AppendToPlaylist(files);

            Media firstMedia = null;
            if (results.Count > 0)
            {
                WinampLibraryEntry firstEntry = results[0];
                firstMedia = new Media()
                    {
                        Title = firstEntry.Title
                    };
            }

            return new MediaQueuedResult()
            {
                QueuedItemCount = results.Count,
                FirstQueuedMedia = firstMedia
            };
        }
    }
}
