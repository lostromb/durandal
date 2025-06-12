/*
** wa_msgids.h (created 14/04/2004 12:23:19 PM)
** Created from wa_ipc.h and resource.h from the language sdk
** by Darren Owen aka DrO
**
** This a simple header file which defines the message ids to allow you to control
** Winamp in keeping with the old frontend.h (R.I.P.)
**
**
** Version History:
**
** v1.0  ::  intial version with ids for Winamp 5.02+
** v1.0a ::  fixed the file to work on compile
** v1.1  ::  added the msg id for 'Manual Playlist Advance'
** v1.2  ::  added in song rating menu items
**
**
** How to use:
**
** To send these, use the format:
**
** SendMessage(hwnd_winamp, WM_COMMAND,command_name,0);
**
** For other languages such as Visual Basic, Pascal, etc you will need to use
** the equivalent calling SendMessage(..) calling convention
**
**
** Notes:
**
** IDs 42000 to 45000 are reserved for gen_ff
** IDs from 45000 to 57000 are reserved for library 
**
*/

#if !_WA_MSGIDS_H_  // this is here just to keep in line with the original WinAmp C++ SDK ;)
#define _WA_MSGIDS_H_ 
namespace MediaControl.Winamp
{

    public enum WA_MsgTypes
    {
        WM_COMMAND = Win32Helpers.WM_COMMAND,
        WM_USER = Win32Helpers.WM_USER,
        WM_COPYDATA = Win32Helpers.WM_COPYDATA,
    }

    public enum WM_COMMAND_MSGS
    {

        WINAMP_FILE_QUIT =                40001,

        /// <summary>
        /// pops up the preferences 
        /// </summary>
        WINAMP_OPTIONS_PREFS =            40012,

        /// <summary>
        /// toggles always on top 
        /// </summary>
        WINAMP_OPTIONS_AOT =              40019,

        WINAMP_FILE_REPEAT =              40022,

        WINAMP_FILE_SHUFFLE =             40023,

        WINAMP_HIGH_PRIORITY =            40025,

        /// <summary>
        /// pops up the load file(s) box 
        /// </summary>
        WINAMP_FILE_PLAY =                40029,

        /// <summary>
        /// toggles the EQ window 
        /// </summary>
        WINAMP_OPTIONS_EQ =               40036,

        WINAMP_OPTIONS_ELAPSED =          40037,

        WINAMP_OPTIONS_REMAINING =        40038,

        /// <summary>
        /// toggles the playlist window 
        /// </summary>
        WINAMP_OPTIONS_PLEDIT =           40040,

        /// <summary>
        /// pops up the about box :) 
        /// </summary>
        WINAMP_HELP_ABOUT =               40041,

        WINAMP_MAINMENU =                 40043,



        /* the following are the five main control buttons, with optionally shift 
        ** or control pressed
        ** (for the exact functions of each, just try it out)
        */
        WINAMP_BUTTON1 =                  40044,

        WINAMP_BUTTON2 =                  40045,

        WINAMP_BUTTON3 =                  40046,

        WINAMP_BUTTON4 =                  40047,

        WINAMP_BUTTON5 =                  40048,

        WINAMP_BUTTON1_SHIFT =            40144,

        WINAMP_BUTTON2_SHIFT =            40145,

        WINAMP_BUTTON3_SHIFT =            40146,

        WINAMP_BUTTON4_SHIFT =            40147,

        WINAMP_BUTTON5_SHIFT =            40148,

        WINAMP_BUTTON1_CTRL =             40154,

        WINAMP_BUTTON2_CTRL =             40155,

        WINAMP_BUTTON3_CTRL =             40156,

        WINAMP_BUTTON4_CTRL =             40157,

        WINAMP_BUTTON5_CTRL =             40158,


        /// <summary>
        /// turns the volume up a little 
        /// </summary>
        WINAMP_VOLUMEUP =                 40058,

        /// <summary>
        /// turns the volume down a little 
        /// </summary>
        WINAMP_VOLUMEDOWN =               40059,

        /// <summary>
        /// fast forwards 5 seconds 
        /// </summary>
        WINAMP_FFWD5S =                   40060,

        /// <summary>
        /// rewinds 5 seconds 
        /// </summary>
        WINAMP_REW5S =                    40061,

        WINAMP_NEXT_WINDOW =              40063,

        WINAMP_OPTIONS_WINDOWSHADE =      40064,

        WINAMP_OPTIONS_DSIZE =            40165,

        IDC_SORT_FILENAME =               40166,

        IDC_SORT_FILETITLE =              40167,

        IDC_SORT_ENTIREFILENAME =         40168,

        IDC_SELECTALL =                   40169,

        IDC_SELECTNONE =                  40170,

        IDC_SELECTINV =                   40171,

        IDM_EQ_LOADPRE =                  40172,

        IDM_EQ_LOADMP3 =                  40173,

        IDM_EQ_LOADDEFAULT =              40174,

        IDM_EQ_SAVEPRE =                  40175,

        IDM_EQ_SAVEMP3 =                  40176,

        IDM_EQ_SAVEDEFAULT =              40177,

        IDM_EQ_DELPRE =                   40178,

        IDM_EQ_DELMP3 =                   40180,

        IDC_PLAYLIST_PLAY =               40184,

        WINAMP_FILE_LOC =                 40185,

        WINAMP_OPTIONS_EASYMOVE =         40186,

        /// <summary>
        /// pops up the load directory box 
        /// </summary>
        WINAMP_FILE_DIR =                 40187,

        WINAMP_EDIT_ID3 =                 40188,

        WINAMP_TOGGLE_AUTOSCROLL =        40189,

        WINAMP_VISSETUP =                 40190,

        WINAMP_PLGSETUP =                 40191,

        WINAMP_VISPLUGIN =                40192,

        WINAMP_JUMP =                     40193,

        WINAMP_JUMPFILE =                 40194,

        WINAMP_JUMP10FWD =                40195,

        WINAMP_JUMP10BACK =               40197,

        WINAMP_PREVSONG =                 40198,

        WINAMP_OPTIONS_EXTRAHQ =          40200,

        ID_PE_NEW =                       40201,

        ID_PE_OPEN =                      40202,

        ID_PE_SAVE =                      40203,

        ID_PE_SAVEAS =                    40204,

        ID_PE_SELECTALL =                 40205,

        ID_PE_INVERT =                    40206,

        ID_PE_NONE =                      40207,

        ID_PE_ID3 =                       40208,

        ID_PE_S_TITLE =                   40209,

        ID_PE_S_FILENAME =                40210,

        ID_PE_S_PATH =                    40211,

        ID_PE_S_RANDOM =                  40212,

        ID_PE_S_REV =                     40213,

        ID_PE_CLEAR =                     40214,

        ID_PE_MOVEUP =                    40215,

        ID_PE_MOVEDOWN =                  40216,

        WINAMP_SELSKIN =                  40219,

        WINAMP_VISCONF =                  40221,

        ID_PE_NONEXIST =                  40222,

        ID_PE_DELETEFROMDISK =            40223,

        ID_PE_CLOSE =                     40224,

        WINAMP_VIS_SETOSC =               40226,

        WINAMP_VIS_SETANA =               40227,

        WINAMP_VIS_SETOFF =               40228,

        WINAMP_VIS_DOTSCOPE =             40229,

        WINAMP_VIS_LINESCOPE =            40230,

        WINAMP_VIS_SOLIDSCOPE =           40231,

        WINAMP_VIS_NORMANA =              40233,

        WINAMP_VIS_FIREANA =              40234,

        WINAMP_VIS_LINEANA =              40235,

        WINAMP_VIS_NORMVU =               40236,

        WINAMP_VIS_SMOOTHVU =             40237,

        WINAMP_VIS_FULLREF =              40238,

        WINAMP_VIS_FULLREF2 =             40239,

        WINAMP_VIS_FULLREF3 =             40240,

        WINAMP_VIS_FULLREF4 =             40241,

        WINAMP_OPTIONS_TOGTIME =          40242,

        EQ_ENABLE =                       40244,

        EQ_AUTO =                         40245,

        EQ_PRESETS =                      40246,

        WINAMP_VIS_FALLOFF0 =             40247,

        WINAMP_VIS_FALLOFF1 =             40248,

        WINAMP_VIS_FALLOFF2 =             40249,

        WINAMP_VIS_FALLOFF3 =             40250,

        WINAMP_VIS_FALLOFF4 =             40251,

        WINAMP_VIS_PEAKS =                40252,

        ID_LOAD_EQF =                     40253,

        ID_SAVE_EQF =                     40254,

        ID_PE_ENTRY =                     40255,

        ID_PE_SCROLLUP =                  40256,

        ID_PE_SCROLLDOWN =                40257,

        WINAMP_MAIN_WINDOW =              40258,

        WINAMP_VIS_PFALLOFF0 =            40259,

        WINAMP_VIS_PFALLOFF1 =            40260,

        WINAMP_VIS_PFALLOFF2 =            40261,

        WINAMP_VIS_PFALLOFF3 =            40262,

        WINAMP_VIS_PFALLOFF4 =            40263,

        ID_PE_TOP =                       40264,

        ID_PE_BOTTOM =                    40265,

        WINAMP_OPTIONS_WINDOWSHADE_PL =   40266,

        EQ_INC1 =                         40267,

        EQ_INC2 =                         40268,

        EQ_INC3 =                         40269,

        EQ_INC4 =                         40270,

        EQ_INC5 =                         40271,

        EQ_INC6 =                         40272,

        EQ_INC7 =                         40273,

        EQ_INC8 =                         40274,

        EQ_INC9 =                         40275,

        EQ_INC10 =                        40276,

        EQ_INCPRE =                       40277,

        EQ_DECPRE =                       40278,

        EQ_DEC1 =                         40279,

        EQ_DEC2 =                         40280,

        EQ_DEC3 =                         40281,

        EQ_DEC4 =                         40282,

        EQ_DEC5 =                         40283,

        EQ_DEC6 =                         40284,

        EQ_DEC7 =                         40285,

        EQ_DEC8 =                         40286,

        EQ_DEC9 =                         40287,

        EQ_DEC10 =                        40288,

        ID_PE_SCUP =                      40289,

        ID_PE_SCDOWN =                    40290,

        WINAMP_REFRESHSKIN =              40291,

        ID_PE_PRINT =                     40292,

        ID_PE_EXTINFO =                   40293,

        WINAMP_PLAYLIST_ADVANCE =         40294,

        WINAMP_VIS_LIN =                  40295,

        WINAMP_VIS_BAR =                  40296,

        WINAMP_OPTIONS_MINIBROWSER =      40298,

        MB_FWD =                          40299,

        MB_BACK =                         40300,

        MB_RELOAD =                       40301,

        MB_OPENMENU =                     40302,

        MB_OPENLOC =                      40303,

        WINAMP_NEW_INSTANCE =             40305,

        MB_UPDATE =                       40309,

        WINAMP_OPTIONS_WINDOWSHADE_EQ =   40310,

        EQ_PANLEFT =                      40313,

        EQ_PANRIGHT =                     40314,

        WINAMP_GETMORESKINS =             40316,

        WINAMP_VIS_OPTIONS =              40317,

        WINAMP_PE_SEARCH =                40318,

        ID_PE_BOOKMARK =                  40319,

        WINAMP_EDIT_BOOKMARKS =           40320,

        WINAMP_MAKECURBOOKMARK =          40321,

        ID_MAIN_PLAY_BOOKMARK_NONE =      40322,

        /// <summary>
        /// starts playing the audio CD in the first CD reader 
        /// </summary>
        ID_MAIN_PLAY_AUDIOCD =            40323,

        /// <summary>
        /// plays the 2nd 
        /// </summary>
        ID_MAIN_PLAY_AUDIOCD2 =           40324,

        /// <summary>
        /// plays the 3rd 
        /// </summary>
        ID_MAIN_PLAY_AUDIOCD3 =           40325,

        /// <summary>
        /// plays the 4th 
        /// </summary>
        ID_MAIN_PLAY_AUDIOCD4 =           40326,

        WINAMP_OPTIONS_VIDEO =            40328,

        ID_VIDEOWND_ZOOMFULLSCREEN =      40329,

        ID_VIDEOWND_ZOOM100 =             40330,

        ID_VIDEOWND_ZOOM200 =             40331,

        ID_VIDEOWND_ZOOM50 =              40332,

        ID_VIDEOWND_VIDEOOPTIONS =        40333,

        WINAMP_MINIMIZE =                 40334,

        ID_PE_FONTBIGGER =                40335,

        ID_PE_FONTSMALLER =               40336,

        WINAMP_VIDEO_TOGGLE_FS =          40337,

        WINAMP_VIDEO_TVBUTTON =           40338,

        WINAMP_LIGHTNING_CLICK =          40339,

        ID_FILE_ADDTOLIBRARY =            40344,

        ID_HELP_HELPTOPICS =              40347,

        ID_HELP_GETTINGSTARTED =          40348,

        ID_HELP_WINAMPFORUMS =            40349,

        ID_PLAY_VOLUMEUP =                40351,

        ID_PLAY_VOLUMEDOWN =              40352,

        ID_PEFILE_OPENPLAYLISTFROMLIBRARY_NOPLAYLISTSINLIBRARY = 40355, 

        ID_PEFILE_ADDFROMLIBRARY =        40356,

        ID_PEFILE_CLOSEPLAYLISTEDITOR =   40357,

        ID_PEPLAYLIST_PLAYLISTPREFERENCES = 40358, 

        ID_MLFILE_NEWPLAYLIST =           40359,

        ID_MLFILE_LOADPLAYLIST =          40360,

        ID_MLFILE_SAVEPLAYLIST =          40361,

        ID_MLFILE_ADDMEDIATOLIBRARY =     40362,

        ID_MLFILE_CLOSEMEDIALIBRARY =     40363,

        ID_MLVIEW_NOWPLAYING =            40364,

        ID_MLVIEW_LOCALMEDIA_ALLMEDIA =   40366,

        ID_MLVIEW_LOCALMEDIA_AUDIO =      40367,

        ID_MLVIEW_LOCALMEDIA_VIDEO =      40368,

        ID_MLVIEW_PLAYLISTS_NOPLAYLISTINLIBRARY = 40369, 

        ID_MLVIEW_INTERNETRADIO =         40370,

        ID_MLVIEW_INTERNETTV =            40371,

        ID_MLVIEW_LIBRARYPREFERENCES =    40372,

        ID_MLVIEW_DEVICES_NOAVAILABLEDEVICE = 40373, 

        ID_MLFILE_IMPORTCURRENTPLAYLIST = 40374, 

        ID_MLVIEW_MEDIA =                 40376,

        ID_MLVIEW_PLAYLISTS =             40377,

        ID_MLVIEW_MEDIA_ALLMEDIA =        40377,

        ID_MLVIEW_DEVICES =               40378,

        ID_FILE_SHOWLIBRARY =             40379,

        ID_FILE_CLOSELIBRARY =            40380,

        ID_POST_PLAY_PLAYLIST =           40381,

        ID_VIS_NEXT =                     40382,

        ID_VIS_PREV =                     40383,

        ID_VIS_RANDOM =                   40384,

        ID_MANAGEPLAYLISTS =              40385,

        ID_PREFS_SKIN_SWITCHTOSKIN =      40386,

        ID_PREFS_SKIN_DELETESKIN =        40387,

        ID_PREFS_SKIN_RENAMESKIN =        40388,

        ID_VIS_FS =                       40389,

        ID_VIS_CFG =                      40390,

        ID_VIS_MENU =                     40391,

        ID_VIS_SET_FS_FLAG =              40392,

        ID_PE_SHOWPLAYING =               40393,

        ID_HELP_REGISTERWINAMPPRO =       40394,

        ID_PE_MANUAL_ADVANCE =            40395,

        WA_SONG_5_STAR_RATING =           40396,

        WA_SONG_4_STAR_RATING =           40397,

        WA_SONG_3_STAR_RATING =           40398,

        WA_SONG_2_STAR_RATING =           40399,

        WA_SONG_1_STAR_RATING =           40400,

        WA_SONG_NO_RATING =               40401,

        PL_SONG_5_STAR_RATING =           40402,

        PL_SONG_4_STAR_RATING =           40403,

        PL_SONG_3_STAR_RATING =           40404,

        PL_SONG_2_STAR_RATING =           40405,

        PL_SONG_1_STAR_RATING =           40406,

        PL_SONG_NO_RATING =               40407,

        AUDIO_TRACK_ONE =                 40408,

        VIDEO_TRACK_ONE =                 40424,


        ID_SWITCH_COLOURTHEME =           44500,

        ID_GENFF_LIMIT =                  45000,
    }

    public enum WM_USER_MSGS
    {
        WA_GET_VERS = 0, // Retrieves the version of Winamp running. Version will be 0x20yx for 2.yx. This is a good way to determine if you did in fact find the right window, etc.
        WA_BEGINPLAYBACK = 100, // Starts playback. A lot like hitting 'play' in Winamp, but not exactly the same
        WA_CLEAR_PLAYLIST = 101, // Clears Winamp's internal playlist.
        WA_PLAY_TRACK = 102, // Begins play of selected track.
        WA_CHANGE_DIR = 103, // Makes Winamp change to the directory C:\\download
        WA_GET_PLAYSTATUS = 104, // Returns the status of playback. If 'ret' is 1, Winamp is playing. If 'ret' is 3, Winamp is paused. Otherwise, playback is stopped.
        WA_GET_PLAYBACK_INFO = 105, // If data is 0, returns the position in milliseconds of playback. If data is 1, returns current track length in seconds. Returns -1 if not playing or if an error occurs.
        WA_SEEK = 106, // Seeks within the current track. The offset is specified in 'data', in milliseconds.
        WA_SAVE_PLAYLIST = 120, // Writes out the current playlist to Winampdir\winamp.m3u, and returns the current position in the playlist.
        WA_PLAYLIST_JUMP = 121, // Sets the playlist position to the position specified in tracks in 'data'.
        WA_SET_VOL = 122, // Sets the volume to 'data', which can be between 0 (silent) and 255 (maximum).
        WA_SET_PANNING = 123, // Sets the panning to 'data', which can be between 0 (all left) and 255 (all right).
        WA_GET_PLAYLIST_LENGTH = 124, // Returns length of the current playlist, in tracks.
        WA_GET_PLAYLIST_POS = 125, // Returns the position in the current playlist, in tracks (requires Winamp 2.05+).
        WA_GET_TRACK_INFO = 126, // Retrieves info about the current playing track. Returns samplerate (i.e. 44100) if 'data' is set to 0, bitrate if 'data' is set to 1, and number of channels if 'data' is set to 2. (requires Winamp 2.05+)
        WA_GET_EQLIZER_DATA_ELEMENT = 127, // Retrieves one element of equalizer data, based on what 'data' is set to.
        // The 10 bands of EQ data. Will return 0-63 (+20db - -20db)
        WA_EQ_1 = 0, 
        WA_EQ_2 = 1,
        WA_EQ_3 = 2,
        WA_EQ_4 = 3,
        WA_EQ_5 = 4,
        WA_EQ_6 = 5,
        WA_EQ_7 = 6,
        WA_EQ_8 = 7,
        WA_EQ_9 = 8,
        WA_EQ_10 = 9,
        ////
        WA_PREAMP = 10,  // The preamp value. Will return 0-63 (+20db - -20db)
        WA_UKNOWN = 11,  // Enabled. Will return zero if disabled, nonzero if enabled.
        WA_AUTOLOAD = 128, // Autoload. Will return zero if disabled, nonzero if enabled. To set an element of equalizer data, simply query which item you wish to set using the message above (127), then call this message with data
        WA_ADD_FILE_TO_BOOKMARK = 129, // Adds the specified file to the Winamp bookmark list
        WA_RESTART = 135, // Restarts Winamp

        WA_SET_SKIN = 200, // Sets the current skin. 'data' points to a string that describes what skin to load, which can either be a directory or a .zip file. If no directory name is specified, the default Winamp skin directory is assumed.
        WA_GET_SKIN = 201, // Retrieves the current skin directory and/or name. 'ret' is a pointer to the Skin name (or NULL if error), and if 'data' is non-NULL, it must point to a string 260 bytes long, which will receive the pathname to where the skin bitmaps are stored (which can be either a skin directory, or a temporary directory when zipped skins are used) (Requires Winamp 2.04+).
        WA_RUN_VIS_PLUG = 202, /* Selects and executes a visualization plug-in. 'data' points to a string which defines which plug-in to execute. The string can be in the following formats:

                vis_whatever.dll Executes the default module in vis_whatever.dll in your plug-ins directory.
                vis_whatever.dll,1 executes the second module in vis_whatever.dll
                C:\path\vis_whatever.dll,1 executes the second module in vis_whatever.dll in another directory */


        WA_GET_TRACK_PATH = 211, // Retrieves (and returns a pointer in 'ret') a string that contains the filename of a playlist entry (indexed by 'data'). Returns NULL if error, or if 'data' is out of range.
        WA_GET_TRACK_TITLE = 212, // Retrieves (and returns a pointer in 'ret') a string that contains the title of a playlist entry (indexed by 'data'). Returns NULL if error, or if 'data' is out of range.
        WA_OPEN_IN_MINIBROWSER = 241, // Opens an new URL in the minibrowser. If the URL is NULL it will open the Minibrowser window
        WA_GET_NET_STATUS = 242, // Returns 1 if the internet connecton is available for Winamp
        WA_UPDATE_TITLE_INFO = 243, // Asks Winamp to update the information about the current title
        WA_SET_PLAYLIST_ITEM = 245, // Sets the current playlist item
        WA_GET_MINIBROWSER_LOC = 246, // Retrives the current Minibrowser URL into the buffer.
        WA_FLUSH_PLAYLIST_CACHE = 247, // Flushes the playlist cache buffer
        WA_MINIBROWSER_NOUPDATE = 248, // Blocks the Minibrowser from updates if value is set to 1
        WA_FORCE_LOAD_URL = 249, // Opens an new URL in the minibrowser (like 241) except that it will work even if 248 is set to 1
        WA_GET_SHUFFLE = 250, // Returns the status of the shuffle option (1 if set)
        WA_GET_REPEAT = 251, // Returns the status of the repeat option (1 if set)
        WA_SET_SHUFFLE = 252, // Sets the status of the suffle option (1 to turn it on)
        WA_SET_REPEAT = 253, // Sets the status of the repeat option (1 to turn it on)
    }


}

#endif