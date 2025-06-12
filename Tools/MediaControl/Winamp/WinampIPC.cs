/*
** Copyright (C) 2003 Nullsoft, Inc.
**
** This software is provided 'as-is', without any express or implied warranty. In no event will the authors be held 
** liable for any damages arising from the use of this software. 
**
** Permission is granted to anyone to use this software for any purpose, including commercial applications, and to 
** alter it and redistribute it freely, subject to the following restrictions:
**
**   1. The origin of this software must not be misrepresented; you must not claim that you wrote the original software. 
**      If you use this software in a product, an acknowledgment in the product documentation would be appreciated but is not required.
**
**   2. Altered source versions must be plainly marked as such, and must not be misrepresented as being the original software.
**
**   3. This notice may not be removed or altered from any source distribution.
**
*/

#define _WA_IPC_H_

//#include "wa_msgids.h"  // part of the original SDK

/*
** This is the modern replacement for the classic 'frontend.h'. Most of these 
** updates are designed for in-process use, i.e. from a plugin.
*/

namespace MediaControl.Winamp
{
    using System;
    using System.Runtime.InteropServices;

#if _WA_IPC_H_


    public enum WA_IPC
    {
    /* message used to sent many messages to winamp's main window. 
    ** most all of the IPC_* messages involve sending the message in the form of:
    **   result = SendMessage(hwnd_winamp,WM_WA_IPC,(parameter),IPC_*);
    */
     WM_WA_IPC = Win32Helpers.WM_USER, 

    /* but some of them use WM_COPYDATA. be afraid.
    */

     IPC_GETVERSION = 0, 

    /* int version = SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_GETVERSION);
    **
    ** Version will be 0x20yx for winamp 2.yx. versions previous to Winamp 2.0
    ** typically (but not always) use 0x1zyx for 1.zx versions. Weird, I know.
    **
    ** For Winamp 5.x it uses 0x50yx for winamp 5.yx
    ** e.g.	5.01 -> 0x5001
    **
    ** note: in 5.02 this will return the same value as for 5.01.
    */

     IPC_GETREGISTEREDVERSION = 770, 

    /* SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_GETREGISTEREDVERSION);
    **
    ** This will open the preferences dialog and show the Winamp Pro option.
    */



    /// <summary>
    /// dont be fooled, this is really the same as enqueufile 
    /// </summary>
     IPC_PLAYFILE = 100,  

     IPC_ENQUEUEFILE = 100,  

    /* sent as a WM_COPYDATA, with IPC_PLAYFILE as the dwData, and the string to play
    ** as the lpData. Just enqueues, does not clear the playlist or change the playback
    ** state.
    */


     IPC_DELETE = 101, 

    /// <summary>
    /// don't use this, it's used internally by winamp when  
    /// </summary>
     IPC_DELETE_INT = 1101, 

                                // dealing with some lame explorer issues.
    /* SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_DELETE);
    ** Use IPC_DELETE to clear Winamp's internal playlist.
    */


    /// <summary>
    /// starts playback. almost like hitting play in Winamp. 
    /// </summary>
     IPC_STARTPLAY = 102,   

    /// <summary>
    /// used internally, don't bother using it (won't be any fun) 
    /// </summary>
     IPC_STARTPLAY_INT = 1102, 



     IPC_CHDIR = 103, 

    /* sent as a WM_COPYDATA, with IPC_CHDIR as the dwData, and the directory to change to
    ** as the lpData. 
    **
    ** COPYDATASTRUCT cds;
    ** cds.dwData = IPC_CHDIR;
    ** cds.lpData = (void *) "c:\\download";
    ** cds.cbData = lstrlen((char *) cds.lpData)+1; // include space for null char
    ** SendMessage(hwnd_winamp,WM_COPYDATA,0,(LPARAM)&cds);
    **
    ** This will make Winamp change to the directory 'C:\download'
    **
    */


     IPC_ISPLAYING = 104, 

    /* int res = SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_ISPLAYING);
    ** If it returns 1, it is playing.
    ** If it returns 3, it is paused.
    ** If it returns 0, it is not playing.
    */


     IPC_GETOUTPUTTIME = 105, 

    /* int res = SendMessage(hwnd_winamp,WM_WA_IPC,mode,IPC_GETOUTPUTTIME);
    ** returns the position in milliseconds of the current track (mode = 0), 
    ** or the track length, in seconds (mode = 1). Returns -1 if not playing or error.
    */


     IPC_JUMPTOTIME = 106, 

    /* (requires Winamp 1.60+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,ms,IPC_JUMPTOTIME);
    ** IPC_JUMPTOTIME sets the position in milliseconds of the 
    ** current song (approximately).
    ** Returns -1 if not playing, 1 on eof, or 0 if successful
    */

     IPC_GETMODULENAME = 109, 

     IPC_EX_ISRIGHTEXE = 666, 

    /* usually shouldnt bother using these, but here goes:
    ** send a WM_COPYDATA with IPC_GETMODULENAME, and an internal
    ** flag gets set, which if you send a normal WM_WA_IPC message with
    ** IPC_EX_ISRIGHTEXE, it returns whether or not that filename
    ** matches. lame, I know.
    */

     IPC_WRITEPLAYLIST = 120, 

    /* (requires Winamp 1.666+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_WRITEPLAYLIST);
    **
    ** IPC_WRITEPLAYLIST writes the current playlist to <winampdir>\\Winamp.m3u,
    ** and returns the current playlist position.
    ** Kinda obsoleted by some of the 2.x new stuff, but still good for when
    ** using a front-end (instead of a plug-in)
    */


     IPC_SETPLAYLISTPOS = 121, 

    /* (requires Winamp 2.0+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,position,IPC_SETPLAYLISTPOS)
    ** IPC_SETPLAYLISTPOS sets the playlist position to 'position'. It
    ** does not change playback or anything, it just sets position, and
    ** updates the view if necessary
    **
    ** If you use SendMessage(hwnd_winamp,WM_COMMAND,WINAMP_BUTTON2,0)
    ** after using IPC_SETPLAYLISTPOS, winamp will start playing the file.
    */


     IPC_SETVOLUME = 122, 

    /* (requires Winamp 2.0+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,volume,IPC_SETVOLUME);
    ** IPC_SETVOLUME sets the volume of Winamp (from 0-255).
    **
    ** If you pass volume as -666 then it will return the current volume.
    */


     IPC_SETPANNING = 123, 

    /* (requires Winamp 2.0+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,panning,IPC_SETPANNING);
    ** IPC_SETPANNING sets the panning of Winamp (from 0 (left) to 255 (right)).
    **
    ** This now appears to work from -127 (left) to 127 (right)
    **
    ** If you pass panning as -666 then it will return the current position.
    */


     IPC_GETLISTLENGTH = 124, 

    /* (requires Winamp 2.0+)
    ** int length = SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_GETLISTLENGTH);
    ** IPC_GETLISTLENGTH returns the length of the current playlist, in
    ** tracks.
    */


     IPC_GETLISTPOS = 125, 

    /* (requires Winamp 2.05+)
    ** int pos=SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_GETLISTPOS);
    ** IPC_GETLISTPOS returns the playlist position. A lot like IPC_WRITEPLAYLIST
    ** only faster since it doesn't have to write out the list. Heh, silly me.
    */


     IPC_GETINFO = 126, 

    /* (requires Winamp 2.05+)
    ** int inf=SendMessage(hwnd_winamp,WM_WA_IPC,mode,IPC_GETINFO);
    ** IPC_GETINFO returns info about the current playing song. The value
    ** it returns depends on the value of 'mode'.
    ** Mode      Meaning
    ** ------------------
    ** 0         Samplerate (i.e. 44100)
    ** 1         Bitrate  (i.e. 128)
    ** 2         Channels (i.e. 2)
    ** 3 (5+)    Video LOWORD=w HIWORD=h
    ** 4 (5+)    > 65536, string (video description)
    */


     IPC_GETEQDATA = 127, 

    /* (requires Winamp 2.05+)
    ** int data=SendMessage(hwnd_winamp,WM_WA_IPC,pos,IPC_GETEQDATA);
    ** IPC_GETEQDATA queries the status of the EQ. 
    ** The value returned depends on what 'pos' is set to:
    ** Value      Meaning
    ** ------------------
    ** 0-9        The 10 bands of EQ data. 0-63 (+20db - -20db)
    ** 10         The preamp value. 0-63 (+20db - -20db)
    ** 11         Enabled. zero if disabled, nonzero if enabled.
    ** 12         Autoload. zero if disabled, nonzero if enabled.
    */


     IPC_SETEQDATA = 128, 

    /* (requires Winamp 2.05+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,pos,IPC_GETEQDATA);
    ** SendMessage(hwnd_winamp,WM_WA_IPC,value,IPC_SETEQDATA);
    ** IPC_SETEQDATA sets the value of the last position retrieved
    ** by IPC_GETEQDATA. This is pretty lame, and we should provide
    ** an extended version that lets you do a MAKELPARAM(pos,value).
    ** someday...

      new (2.92+): 
        if the high byte is set to 0xDB, then the third byte specifies
        which band, and the bottom word specifies the value.
    */

     IPC_ADDBOOKMARK = 129, 

    /* (requires Winamp 2.4+)
    ** Sent as a WM_COPYDATA, using IPC_ADDBOOKMARK, adds the specified
    ** file/url to the Winamp bookmark list.
    */
    /*
    In winamp 5+, we use this as a normal WM_WA_IPC and the string:

      "filename\0title\0"

      to notify the library/bookmark editor that a bookmark
    was added. Note that using this message in this context does not
    actually add the bookmark.
    do not use :)
    */


     IPC_INSTALLPLUGIN = 130, 

    /* not implemented, but if it was you could do a WM_COPYDATA with 
    ** a path to a .wpz, and it would install it.
    */


     IPC_RESTARTWINAMP = 135, 

    /* (requires Winamp 2.2+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_RESTARTWINAMP);
    ** IPC_RESTARTWINAMP will restart Winamp (isn't that obvious ? :)
    */


     IPC_ISFULLSTOP = 400, 

    /* (requires winamp 2.7+ I think)
    ** ret=SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_ISFULLSTOP);
    ** useful for when you're an output plugin, and you want to see
    ** if the stop/close is a full stop, or just between tracks.
    ** returns nonzero if it's full, zero if it's just a new track.
    */


     IPC_INETAVAILABLE = 242, 

    /* (requires Winamp 2.05+)
    ** val=SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_INETAVAILABLE);
    ** IPC_INETAVAILABLE will return 1 if the Internet connection is available for Winamp.
    */


     IPC_UPDTITLE = 243, 

    /* (requires Winamp 2.2+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_UPDTITLE);
    ** IPC_UPDTITLE will ask Winamp to update the informations about the current title.
    */


     IPC_REFRESHPLCACHE = 247, 

    /* (requires Winamp 2.2+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_REFRESHPLCACHE);
    ** IPC_REFRESHPLCACHE will flush the playlist cache buffer.
    ** (send this if you want it to go refetch titles for tracks)
    */


     IPC_GET_SHUFFLE = 250, 

    /* (requires Winamp 2.4+)
    ** val=SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_GET_SHUFFLE);
    **
    ** IPC_GET_SHUFFLE returns the status of the Shuffle option (1 if set)
    */


     IPC_GET_REPEAT = 251, 

    /* (requires Winamp 2.4+)
    ** val=SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_GET_REPEAT);
    **
    ** IPC_GET_REPEAT returns the status of the Repeat option (1 if set)
    */


     IPC_SET_SHUFFLE = 252, 

    /* (requires Winamp 2.4+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,value,IPC_SET_SHUFFLE);
    **
    ** IPC_SET_SHUFFLE sets the status of the Shuffle option (1 to turn it on)
    */


     IPC_SET_REPEAT = 253, 

    /* (requires Winamp 2.4+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,value,IPC_SET_REPEAT);
    **
    ** IPC_SET_REPEAT sets the status of the Repeat option (1 to turn it on)
    */


    /// <summary>
    /// 0xdeadbeef to disable 
    /// </summary>
     IPC_ENABLEDISABLE_ALL_WINDOWS = 259,

    /* (requires Winamp 2.9+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,enable?0:0xdeadbeef,IPC_ENABLEDISABLE_ALL_WINDOWS);
    ** sending with 0xdeadbeef as the param disables all winamp windows, 
    ** any other values will enable all winamp windows.
    */


     IPC_GETWND = 260, 

    /* (requires Winamp 2.9+)
    ** HWND h=SendMessage(hwnd_winamp,WM_WA_IPC,IPC_GETWND_xxx,IPC_GETWND);
    ** returns the HWND of the window specified.
    */
      //#define IPC_GETWND_EQ 0 // use one of these for the param
      //#define IPC_GETWND_PE 1
      //#define IPC_GETWND_MB 2
      //#define IPC_GETWND_VIDEO 3
    /// <summary>
    /// same param as IPC_GETWND 
    /// </summary>
     IPC_ISWNDVISIBLE = 261, 





    /************************************************************************
    ***************** in-process only (WE LOVE PLUGINS)
    ************************************************************************/


     IPC_SETSKIN = 200, 

    /* (requires Winamp 2.04+, only usable from plug-ins (not external apps))
    ** SendMessage(hwnd_winamp,WM_WA_IPC,(WPARAM)"skinname",IPC_SETSKIN);
    ** IPC_SETSKIN sets the current skin to "skinname". Note that skinname 
    ** can be the name of a skin, a skin .zip file, with or without path. 
    ** If path isn't specified, the default search path is the winamp skins 
    ** directory.
    */


     IPC_GETSKIN = 201, 

    /* (requires Winamp 2.04+, only usable from plug-ins (not external apps))
    ** SendMessage(hwnd_winamp,WM_WA_IPC,(WPARAM)skinname_buffer,IPC_GETSKIN);
    ** IPC_GETSKIN puts the directory where skin bitmaps can be found 
    ** into  skinname_buffer.
    ** skinname_buffer must be MAX_PATH characters in length.
    ** When using a .zip'd skin file, it'll return a temporary directory
    ** where the ZIP was decompressed.
    */


     IPC_EXECPLUG = 202, 

    /* (requires Winamp 2.04+, only usable from plug-ins (not external apps))
    ** SendMessage(hwnd_winamp,WM_WA_IPC,(WPARAM)"vis_file.dll",IPC_EXECPLUG);
    ** IPC_EXECPLUG executes a visualization plug-in pointed to by WPARAM.
    ** the format of this string can be:
    ** "vis_whatever.dll"
    ** "vis_whatever.dll,0" // (first mod, file in winamp plug-in dir)
    ** "C:\\dir\\vis_whatever.dll,1" 
    */


     IPC_GETPLAYLISTFILE = 211, 

    /* (requires Winamp 2.04+, only usable from plug-ins (not external apps))
    ** char *name=SendMessage(hwnd_winamp,WM_WA_IPC,index,IPC_GETPLAYLISTFILE);
    ** IPC_GETPLAYLISTFILE gets the filename of the playlist entry [index].
    ** returns a pointer to it. returns NULL on error.
    */


     IPC_GETPLAYLISTTITLE = 212, 

    /* (requires Winamp 2.04+, only usable from plug-ins (not external apps))
    ** char *name=SendMessage(hwnd_winamp,WM_WA_IPC,index,IPC_GETPLAYLISTTITLE);
    **
    ** IPC_GETPLAYLISTTITLE gets the title of the playlist entry [index].
    ** returns a pointer to it. returns NULL on error.
    */


     IPC_GETHTTPGETTER = 240, 

    /* retrieves a function pointer to a HTTP retrieval function.
    ** if this is unsupported, returns 1 or 0.
    ** the function should be:
    ** int (*httpRetrieveFile)(HWND hwnd, char *url, char *file, char *dlgtitle);
    ** if you call this function, with a parent window, a URL, an output file, and a dialog title,
    ** it will return 0 on successful download, 1 on error.
    */


     IPC_MBOPEN = 241, 

    /* (requires Winamp 2.05+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_MBOPEN);
    ** SendMessage(hwnd_winamp,WM_WA_IPC,(WPARAM)url,IPC_MBOPEN);
    ** IPC_MBOPEN will open a new URL in the minibrowser. if url is NULL, it will open the Minibrowser window.
    */



     IPC_CHANGECURRENTFILE = 245, 

    /* (requires Winamp 2.05+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,(WPARAM)file,IPC_CHANGECURRENTFILE);
    ** IPC_CHANGECURRENTFILE will set the current playlist item.
    */


     IPC_GETMBURL = 246, 

    /* (requires Winamp 2.2+)
    ** char buffer[4096]; // Urls can be VERY long
    ** SendMessage(hwnd_winamp,WM_WA_IPC,(WPARAM)buffer,IPC_GETMBURL);
    ** IPC_GETMBURL will retrieve the current Minibrowser URL into buffer.
    ** buffer must be at least 4096 bytes long.
    */


     IPC_MBBLOCK = 248, 

    /* (requires Winamp 2.4+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,value,IPC_MBBLOCK);
    **
    ** IPC_MBBLOCK will block the Minibrowser from updates if value is set to 1
    */

     IPC_MBOPENREAL = 249, 

    /* (requires Winamp 2.4+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,(WPARAM)url,IPC_MBOPENREAL);
    **
    ** IPC_MBOPENREAL works the same as IPC_MBOPEN except that it will works even if 
    ** IPC_MBBLOCK has been set to 1
    */

     IPC_ADJUST_OPTIONSMENUPOS = 280, 

    /* (requires Winamp 2.9+)
    ** int newpos=SendMessage(hwnd_winamp,WM_WA_IPC,(WPARAM)adjust_offset,IPC_ADJUST_OPTIONSMENUPOS);
    ** moves where winamp expects the Options menu in the main menu. Useful if you wish to insert a
    ** menu item above the options/skins/vis menus.
    */

     IPC_GET_HMENU = 281, 

    /* (requires Winamp 2.9+)
    ** HMENU hMenu=SendMessage(hwnd_winamp,WM_WA_IPC,(WPARAM)0,IPC_GET_HMENU);
    ** values for data:
    ** 0 : main popup menu 
    ** 1 : main menubar file menu
    ** 2 : main menubar options menu
    ** 3 : main menubar windows menu
    ** 4 : main menubar help menu
    ** other values will return NULL.
    */

     IPC_GET_EXTENDED_FILE_INFO = 290, //pass,a,pointer,to,the,following,struct,in,wParam,

     IPC_GET_EXTENDED_FILE_INFO_HOOKABLE = 296, 

    /* (requires Winamp 2.9+)
    ** to use, create an extendedFileInfoStruct, point the values filename and metadata to the
    ** filename and metadata field you wish to query, and ret to a buffer, with retlen to the
    ** length of that buffer, and then SendMessage(hwnd_winamp,WM_WA_IPC,&struct,IPC_GET_EXTENDED_FILE_INFO);
    ** the results should be in the buffer pointed to by ret.
    ** returns 1 if the decoder supports a getExtendedFileInfo method
    */


    /*

    {
    /*basicFileInfoStruct file;
    char buf[MAX_PATH];
	    lstrcpyn(buf,"D:\\CDex Ripped\\Underworld\\Underworld 1992 - 2002 CD2\\a.mp3",sizeof(buf));
	    file.filename = buf;
	    file.quickCheck = 0;
	    SendMessage(hwnd_winamp,WM_WA_IPC,(WPARAM)&file,IPC_GET_BASIC_FILE_INFO);*/

    // Get the file info
    /*char title[1096], buf[MAX_PATH];
    basicFileInfoStruct fileInfo;

    lstrcpyn(buf,"D:\\CDex Ripped\\Underworld\\Underworld 1992 - 2002 CD2\\a.mp3",sizeof(buf));
    fileInfo.filename = buf;
    fileInfo.title = title;
    fileInfo.titlelen = sizeof(title);
    fileInfo.quickCheck = 0;
    SendMessage(hwnd_winamp,WM_WA_IPC,(WPARAM)&fileInfo, IPC_GET_BASIC_FILE_INFO);

	    MessageBox(0,fileInfo.title,0,0);
    }

    */

     IPC_GET_EXTLIST = 292, //returns,doublenull,delimited.,GlobalFree(),it,when,done.,if,data,is,0,,returns,raw,extlist,,if,1,,returns,something,suitable,for,getopenfilename,


     IPC_INFOBOX = 293, 


     IPC_SET_EXTENDED_FILE_INFO = 294, //pass,a,pointer,to,the,a,extendedFileInfoStruct,in,wParam,

    /* (requires Winamp 2.9+)
    ** to use, create an extendedFileInfoStruct, point the values filename and metadata to the
    ** filename and metadata field you wish to write in ret. (retlen is not used). and then 
    ** SendMessage(hwnd_winamp,WM_WA_IPC,&struct,IPC_SET_EXTENDED_FILE_INFO);
    ** returns 1 if the metadata is supported
    ** Call IPC_WRITE_EXTENDED_FILE_INFO once you're done setting all the metadata you want to update
    */

     IPC_WRITE_EXTENDED_FILE_INFO = 295,  

    /* (requires Winamp 2.9+)
    ** writes all the metadata set thru IPC_SET_EXTENDED_FILE_INFO to the file
    ** returns 1 if the file has been successfully updated, 0 if error
    */

     IPC_FORMAT_TITLE = 297, 


     IPC_GETUNCOMPRESSINTERFACE = 331, 

    /* SendMessage(hwnd_winamp,WM_WA_IPC,param,IPC_GETUNCOMPRESSINTERFACE)
    **
    ** This returns a function pointer to uncompress() when param = 0.
    ** int (*uncompress)(unsigned char *dest, unsigned long *destLen,
    **                   const unsigned char *source, unsigned long sourceLen);
    ** which is taken right out of zlib and is useful for decompressing zlibbed data.
    **
    ** If you pass param = 0x10100000 the function will return a wa_inflate_struct*
    ** to an inflate API which gives you more control over what you can do.
    ** e.g.
    ** wa_inflate_struct* uncompiface = (wa_inflate_struct*)SendMessage(hwnd_winamp,WM_WA_IPC,0x10100000,IPC_GETUNCOMPRESSINTERFACE);
    */


     IPC_GET_BASIC_FILE_INFO = 291, //pass,a,pointer,to,the,Basic File Info struct,in,wParam,

     IPC_ADD_PREFS_DLG = 332, 

     IPC_REMOVE_PREFS_DLG = 333, 

    /* (requires Winamp 2.9+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,&prefsRec,IPC_ADD_PREFS_DLG);
    ** SendMessage(hwnd_winamp,WM_WA_IPC,&prefsRec,IPC_REMOVE_PREFS_DLG);
    **
    ** IPC_ADD_PREFS_DLG:
    ** To use you need to allocate a prefsDlgRec structure (either on the heap or 
    ** with some global data but NOT on the stack) and then initialise the members
    ** of the structure (see the definition of the prefsDlgRec structure).
    **    hInst to the DLL instance where the resource is located
    **    dlgID to the ID of the dialog,
    **    proc to the window procedure for the dialog
    **    name to the name of the prefs page in the prefs.
    **    where to 0 (eventually we may add more options)
    ** then, SendMessage(hwnd_winamp,WM_WA_IPC,&prefsRec,IPC_ADD_PREFS_DLG);
    **
    ** example:
    **
    ** prefsDlgRec* prefsRec = 0;
    **   prefsRec = GlobalAlloc(GPTR,sizeof(prefsDlgRec));
    **   prefsRec->hInst = hInst;
    **   prefsRec->dlgID = IDD_PREFDIALOG;
    **   prefsRec->name = "Pref Page";
    **   prefsRec->where = 0;
    **   prefsRec->proc = PrefsPage;
    **   SendMessage(hwnd_winamp,WM_WA_IPC,&prefsRec,IPC_ADD_PREFS_DLG);
    **
    **
    ** IPC_REMOVE_PREFS_DLG:
    ** To use you pass the address of the same prefsRec you used when adding the
    ** prefs page though you shouldn't really ever have to do this.
    */

     IPC_OPENPREFSTOPAGE = 380, 

    /* SendMessage(hwnd_winamp,WM_WA_IPC,&prefsRec,IPC_OPENPREFSTOPAGE);
    **
    ** There are two ways of opening a preferences page.
    ** The first is to pass an id of a builtin preferences page (see below for ids)
    ** or a &prefsDlgRec of the preferences page to open and this is normally done
    ** if you are opening a prefs page you added yourself
    **
    ** If the page id does not or the &prefsRec is not valid then the prefs dialog
    ** will be opened to the first page available (usually the Winamp Pro page).
    **
    ** (requires Winamp 5.04+)
    ** Passing -1 will open open the prefs dialog to the last page viewed
    **
    ** Note: v5.0 to 5.03 had a bug in this api
    **       
    ** On the first call then the correct prefs page would be opened to but on the
    ** next call the prefs dialog would be brought to the front but the page would
    ** not be changed to the specified.
    ** In 5.04+ it will change to the prefs page specified if the prefs dialog is
    ** already open.
    */

    /* Builtin Preference page ids (valid for 5.0+)
    ** (stored in the lParam member of the TVITEM structure from the tree item)
    **
    ** These can be useful if you want to detect a specific prefs page and add
    ** things to it yourself or something like that ;)
    **
    ** Winamp Pro           20
    ** General Preferences  0
    ** File Types           1
    ** Playlist             23
    ** Titles               21
    ** Video                24
    ** Skins                40
    ** Classic Skins        22
    ** Plugins              30
    ** Input                31
    ** Output               32
    ** Visualisation        33
    ** DSP/Effect           34
    ** General Purpose      35
    **
    ** Note:
    ** Custom page ids begin from 60
    ** The value of the normal custom pages (Global Hotkeys, Jump To File, etc) is
    ** not guaranteed since it depends on the order in which the plugins are loaded
    ** which can change on different systems.
    **
    ** Global Hotkeys, Jump To File, Media Library (under General Preferences),
    ** Media Library (under Plugins), CD Ripping and Modern Skins are custom pages
    ** created by the plugins shipped with Winamp
    */


     IPC_GETINIFILE = 334, 

    /* SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_GETINIFILE);
    **
    ** This returns a pointer to the full file path of winamp.ini.
    */

     IPC_GETINIDIRECTORY = 335, 

    /* SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_GETINIDIRECTORY);
    **
    ** This returns a pointer to the directory where winamp.ini can be found and is
    ** useful if you want store config files but you don't want to use winamp.ini.
    */

     IPC_SPAWNBUTTONPOPUP = 361, 

    /* SendMessage(hwnd_winamp,WM_WA_IPC,param,IPC_SPAWNBUTTONPOPUP);
    **
    ** This will show the specified menu at the current mouse position.
    ** param = 0  ::  eject
    **         1  ::  previous
    **         2  ::  next
    **         3  ::  pause
    **         4  ::  play
    **         5  ::  stop
    */


    /// <summary>
    /// pass a HWND to a parent, returns a HGLOBAL that needs to be freed with GlobalFree(), if successful 
    /// </summary>
     IPC_OPENURLBOX = 360, 

    /// <summary>
    /// pass a HWND to a parent 
    /// </summary>
     IPC_OPENFILEBOX = 362, 

    /// <summary>
    /// pass a HWND to a parent 
    /// </summary>
     IPC_OPENDIRBOX = 363, 


    // pass an HWND to a parent. call this if you take over the whole UI so that the dialogs are not appearing on the
    // bottom right of the screen since the main winamp window is at 3000x3000, call again with NULL to reset
     IPC_SETDIALOGBOXPARENT = 364,  




    // pass 0 for a copy of the skin HBITMAP
    // pass 1 for name of font to use for playlist editor likeness
    // pass 2 for font charset
    // pass 3 for font size
     IPC_GET_GENSKINBITMAP = 503, 



    /// <summary>
    /// pass an embedWindowState 
    /// </summary>
     IPC_GET_EMBEDIF = 505, 

    // returns an HWND embedWindow(embedWindowState *); if the data is NULL, otherwise returns the HWND directly

    /// <summary>
    /// set this bit in embedWindowState.flags to keep window from being resizable 
    /// </summary>
     EMBED_FLAGS_NORESIZE = 1, 

    /// <summary>
    /// set this bit in embedWindowState.flags to make gen_ff turn transparency off for this wnd 
    /// </summary>
     EMBED_FLAGS_NOTRANSPARENCY = 2, 



     IPC_EMBED_ENUM = 532, 



     IPC_EMBED_ISVALID = 533, 


     IPC_CONVERTFILE = 506, 

    /* (requires Winamp 2.92+)
    ** Converts a given file to a different format (PCM, MP3, etc...)
    ** To use, pass a pointer to a waFileConvertStruct struct
    ** This struct can be either on the heap or some global
    ** data, but NOT on the stack. At least, until the conversion is done.
    **
    ** eg: SendMessage(hwnd_winamp,WM_WA_IPC,&myConvertStruct,IPC_CONVERTFILE);
    **
    ** Return value:
    ** 0: Can't start the conversion. Look at myConvertStruct->error for details.
    ** 1: Conversion started. Status messages will be sent to the specified callbackhwnd.
    **    Be sure to call IPC_CONVERTFILE_END when your callback window receives the
    **    IPC_CB_CONVERT_DONE message.
    */


     IPC_CONVERTFILE_END = 507, 

    /* (requires Winamp 2.92+)
    ** Stop/ends a convert process started from IPC_CONVERTFILE
    ** You need to call this when you receive the IPC_CB_CONVERTDONE message or when you
    ** want to abort a conversion process
    **
    ** eg: SendMessage(hwnd_winamp,WM_WA_IPC,&myConvertStruct,IPC_CONVERTFILE_END);
    **
    ** No return value
    */

     IPC_CONVERT_CONFIG = 508, 

     IPC_CONVERT_CONFIG_END = 509, 

     IPC_BURN_CD = 511, 

    // return TRUE if you hook this
     IPC_HOOK_TITLES = 850, 


     IPC_GETSADATAFUNC = 800,  

    // 0: returns a char *export_sa_get() that returns 150 bytes of data
    // 1: returns a export_sa_setreq(int want);

     IPC_ISMAINWNDVISIBLE = 900, 



     IPC_SETPLEDITCOLORS = 920, 



    // the following IPC use waSpawnMenuParms as parameter
     IPC_SPAWNEQPRESETMENU = 933, 

     IPC_SPAWNFILEMENU = 934, //menubar,

     IPC_SPAWNOPTIONSMENU = 935,//menubar,

     IPC_SPAWNWINDOWSMENU = 936, //menubar,

     IPC_SPAWNHELPMENU = 937, //menubar,

     IPC_SPAWNPLAYMENU = 938, //menubar,

     IPC_SPAWNPEFILEMENU = 939, //menubar,

     IPC_SPAWNPEPLAYLISTMENU = 940, //menubar,

     IPC_SPAWNPESORTMENU = 941, //menubar,

     IPC_SPAWNPEHELPMENU = 942, //menubar,

     IPC_SPAWNMLFILEMENU = 943, //menubar,

     IPC_SPAWNMLVIEWMENU = 944, //menubar,

     IPC_SPAWNMLHELPMENU = 945, //menubar,

     IPC_SPAWNPELISTOFPLAYLISTS = 946,


    // system tray sends this (you might want to simulate it)
     WM_WA_SYSTRAY = Win32Helpers.WM_USER+1, 


    // input plugins send this when they are done playing back
     WM_WA_MPEG_EOF = Win32Helpers.WM_USER+2, 


    IPC_CONVERT_CONFIG_ENUMFMTS = 510,

    IPC_CONVERT_SET_PRIORITY = 512,

    //// video stuff

    /// <summary>
    /// returns >1 if playing, 0 if not, 1 if old version (so who knows):) 
    /// </summary>
     IPC_IS_PLAYING_VIDEO = 501, 

    /// <summary>
    /// see below for IVideoOutput interface 
    /// </summary>
     IPC_GET_IVIDEOOUTPUT = 500, 

     //VIDEO_MAKETYPE(A,B,C,D) = ((A) |,((B)<<8),|,((C)<<16),|,((D)<<24)),// this i'm not sure how to convert

     VIDUSER_SET_INFOSTRING = 0x1000, 

     VIDUSER_GET_VIDEOHWND =  0x1001,

     VIDUSER_SET_VFLIP =      0x1002,

    /// <summary>
    /// give your ITrackSelector interface as param2 
    /// </summary>
     VIDUSER_SET_TRACKSELINTERFACE = 0x1003, 

    // these messages are callbacks that you can grab by subclassing the winamp window

    // wParam = 
    /// <summary>
    /// use one of these for the param 
    /// </summary>
     IPC_CB_WND_EQ = 0, 

     IPC_CB_WND_PE = 1, 

     IPC_CB_WND_MB = 2, 

     IPC_CB_WND_VIDEO = 3, 

     IPC_CB_WND_MAIN = 4, 


     IPC_CB_ONSHOWWND = 600,  

     IPC_CB_ONHIDEWND = 601,  


     IPC_CB_GETTOOLTIP = 602, 


     IPC_CB_MISC = 603, 

        //#define IPC_CB_MISC_TITLE 0
        //#define IPC_CB_MISC_VOLUME 1 // volume/pan
        //#define IPC_CB_MISC_STATUS 2
        //#define IPC_CB_MISC_EQ 3
        //#define IPC_CB_MISC_INFO 4
        //#define IPC_CB_MISC_VIDEOINFO 5

    /// <summary>
    /// param value goes from 0 to 100 (percent) 
    /// </summary>
     IPC_CB_CONVERT_STATUS = 604, 

     IPC_CB_CONVERT_DONE =   605,


     IPC_ADJUST_FFWINDOWSMENUPOS = 606, 

    /* (requires Winamp 2.9+)
    ** int newpos=SendMessage(hwnd_winamp,WM_WA_IPC,(WPARAM)adjust_offset,IPC_ADJUST_FFWINDOWSMENUPOS);
    ** moves where winamp expects the freeform windows in the menubar windows main menu. Useful if you wish to insert a
    ** menu item above extra freeform windows.
    */

     IPC_ISDOUBLESIZE = 608, 


     IPC_ADJUST_FFOPTIONSMENUPOS = 609, 

    /* (requires Winamp 2.9+)
    ** int newpos=SendMessage(hwnd_winamp,WM_WA_IPC,(WPARAM)adjust_offset,IPC_ADJUST_FFOPTIONSMENUPOS);
    ** moves where winamp expects the freeform preferences item in the menubar windows main menu. Useful if you wish to insert a
    ** menu item above preferences item.
    */

    /// <summary>
    /// returns 0 if displaying elapsed time or 1 if displaying remaining time 
    /// </summary>
     IPC_GETTIMEDISPLAYMODE = 610, 


    /// <summary>
    /// param is hwnd, setting this allows you to receive ID_VIS_NEXT/PREVOUS/RANDOM/FS wm_commands 
    /// </summary>
     IPC_SETVISWND = 611, 

     ID_VIS_NEXT =                     40382,

     ID_VIS_PREV =                     40383,

     ID_VIS_RANDOM =                   40384,

     ID_VIS_FS =                       40389,

     ID_VIS_CFG =                      40390,

     ID_VIS_MENU =                     40391,


    /// <summary>
    /// returns the vis cmd handler hwnd 
    /// </summary>
     IPC_GETVISWND = 612, 

     IPC_ISVISRUNNING = 613, 

    /// <summary>
    /// param is status of random 
    /// </summary>
     IPC_CB_VISRANDOM = 628, 


    /// <summary>
    /// sent by winamp to winamp, trap it if you need it. width=HIWORD(param), height=LOWORD(param) 
    /// </summary>
     IPC_SETIDEALVIDEOSIZE = 614,


     IPC_GETSTOPONVIDEOCLOSE = 615, 

     IPC_SETSTOPONVIDEOCLOSE = 616, 

     IPC_TRANSLATEACCELERATOR = 617, 

     IPC_CB_ONTOGGLEAOT = 618, 


     IPC_GETPREFSWND = 619, 


    /// <summary>
    /// data is a pointer to a POINT structure that holds width & height 
    /// </summary>
     IPC_SET_PE_WIDTHHEIGHT = 620, 


     IPC_GETLANGUAGEPACKINSTANCE = 621,


    /// <summary>
    /// data is a string, ie: "04:21/45:02" 
    /// </summary>
     IPC_CB_PEINFOTEXT = 622, 


    /// <summary>
    /// output plugin was changed in config 
    /// </summary>
     IPC_CB_OUTPUTCHANGED = 623, 


     IPC_GETOUTPUTPLUGIN = 625, 


     IPC_SETDRAWBORDERS = 626, 

     IPC_DISABLESKINCURSORS = 627, 

     IPC_CB_RESETFONT = 629, 


    /// <summary>
    /// returns 1 if video or vis is in fullscreen mode 
    /// </summary>
     IPC_IS_FULLSCREEN = 630, 

    /// <summary>
    /// a vis should send this message with 1/as param to notify winamp that it has gone to or has come back from fullscreen mode 
    /// </summary>
     IPC_SET_VIS_FS_FLAG = 631, 


     IPC_SHOW_NOTIFICATION = 632, 


     IPC_GETSKININFO = 633, 


     IPC_GET_MANUALPLADVANCE = 634, 

    /* (requires Winamp 5.03+)
    ** val=SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_GET_MANUALPLADVANCE);
    **
    ** IPC_GET_MANUALPLADVANCE returns the status of the Manual Playlist Advance (1 if set)
    */

     IPC_SET_MANUALPLADVANCE = 635, 

    /* (requires Winamp 5.03+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,value,IPC_SET_MANUALPLADVANCE);
    **
    ** IPC_SET_MANUALPLADVANCE sets the status of the Manual Playlist Advance option (1 to turn it on)
    */

     IPC_GET_NEXT_PLITEM = 636, 

    /* (requires Winamp 5.04+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_EOF_GET_NEXT_PLITEM);
    **
    ** Sent to Winamp's main window when an item has just finished playback or the next button has been pressed and 
    ** requesting the new playlist item number to go to.
    ** Mainly used by gen_jumpex. Subclass this message in your application to return the new item number. 
    ** -1 for normal winamp operation (default) or the new item number in the playlist to play.
    */

     IPC_GET_PREVIOUS_PLITEM = 637, 

    /* (requires Winamp 5.04+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_EOF_GET_PREVIOUS_PLITEM);
    **
    ** Sent to Winamp's main window when the previous button has been pressed and Winamp is requesting the new playlist item number to go to.
    ** Mainly used by gen_jumpex. Subclass this message in your application to return the new item number. 
    ** -1 for normal winamp operation (default) or the new item number in the playlist to play.
    */

     IPC_IS_WNDSHADE = 638,  

    /* (requires Winamp 5.04+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,wnd,IPC_IS_WNDSHADE);
    **
    ** 'wnd' is window id as defined for IPC_GETWND, or -1 for main window
    ** Returns 1 if wnd is set to winshade mode, or 0 if it is not
    */

     IPC_SETRATING = 639,  

    /* (requires Winamp 5.04+ with ML)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,rating,IPC_SETRATING);
    ** 'rating' is an int value from 0 (no rating) to 5 
    */

     IPC_GETRATING = 640,  

    /* (requires Winamp 5.04+ with ML)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_GETRATING);
    ** returns the current item's rating
    */

     IPC_GETNUMAUDIOTRACKS = 641, 

    /* (requires Winamp 5.04+)
    ** int n = SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_GETNUMAUDIOTRACKS);
    ** returns the number of audio tracks for the currently playing item
    */

     IPC_GETNUMVIDEOTRACKS = 642, 

    /* (requires Winamp 5.04+)
    ** int n = SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_GETNUMVIDEOTRACKS);
    ** returns the number of video tracks for the currently playing item
    */

     IPC_GETAUDIOTRACK = 643, 

    /* (requires Winamp 5.04+)
    ** int cur = SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_GETAUDIOTRACK);
    ** returns the id of the current audio track for the currently playing item
    */

     IPC_GETVIDEOTRACK = 644, 

    /* (requires Winamp 5.04+)
    ** int cur = SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_GETVIDEOTRACK);
    ** returns the id of the current video track for the currently playing item
    */

     IPC_SETAUDIOTRACK = 645, 

    /* (requires Winamp 5.04+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,track,IPC_SETAUDIOTRACK);
    ** switch the currently playing item to a new audio track 
    */

     IPC_SETVIDEOTRACK = 646, 

    /* (requires Winamp 5.04+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,track,IPC_SETVIDEOTRACK);
    ** switch the currently playing item to a new video track 
    */

     IPC_PUSH_DISABLE_EXIT = 647, 

    /* (requires Winamp 5.04+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_PUSH_DISABLE_EXIT );
    ** lets you disable or re-enable the UI exit functions (close button, 
    ** context menu, alt-f4).
    ** call IPC_POP_DISABLE_EXIT when you are done doing whatever required 
    ** preventing exit
    */

     IPC_POP_DISABLE_EXIT =  648,

    /* (requires Winamp 5.04+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_POP_DISABLE_EXIT );
    ** see IPC_PUSH_DISABLE_EXIT
    */

     IPC_IS_EXIT_ENABLED = 649, 

    /* (requires Winamp 5.04+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_IS_EXIT_ENABLED);
    ** returns 0 if exit is disabled, 1 otherwise
    */

     IPC_IS_AOT = 650, 

    /* (requires Winamp 5.04+)
    ** SendMessage(hwnd_winamp,WM_WA_IPC,0,IPC_IS_AOT);
    ** returns status of always on top flag. note: this may not match the actual
    ** TOPMOST window flag while another fullscreen application is focused
    */

    // >>>>>>>>>>> Next is 651

     IPC_PLCMD =  1000, 


     PLCMD_ADD =  0,

     PLCMD_REM =  1,

     PLCMD_SEL =  2,

     PLCMD_MISC = 3, 

     PLCMD_LIST = 4, 


     IPC_MBCMD =  1001, 


     MBCMD_BACK =    0,

     MBCMD_FORWARD = 1,

     MBCMD_STOP =    2,

     MBCMD_RELOAD =  3,

     MBCMD_MISC =  4,


     IPC_VIDCMD = 1002,  


     VIDCMD_FULLSCREEN = 0, 

     VIDCMD_1X =         1,

     VIDCMD_2X =         2,

     VIDCMD_LIB =        3,

     VIDPOPUP_MISC =     4,


     IPC_MBURL =       1003,//sets,the,URL,

     IPC_MBGETCURURL = 1004, //copies,the,current,URL,into,wParam,(have,a,4096,buffer,ready),

     IPC_MBGETDESC =   1005,//copies,the,current,URL,description,into,wParam,(have,a,4096,buffer,ready),

     IPC_MBCHECKLOCFILE = 1006, //checks,that,the,link,file,is,up,to,date,(otherwise,updates,it).,wParam=parent,HWND,

     IPC_MBREFRESH =   1007,//refreshes,the,"now,playing",view,in,the,library,

     IPC_MBGETDEFURL = 1008, //copies,the,default,URL,into,wParam,(have,a,4096,buffer,ready),


    /// <summary>
    /// updates library count status 
    /// </summary>
     IPC_STATS_LIBRARY_ITEMCNT = 1300, 


    // IPC 2000-3000 reserved for freeform messages, see gen_ff/ff_ipc.h
     IPC_FF_FIRST = 2000, 

     IPC_FF_LAST =  3000,


     IPC_GETDROPTARGET = 3001, 


    /// <summary>
    /// sent to main wnd whenever the playlist is modified 
    /// </summary>
     IPC_PLAYLIST_MODIFIED = 3002, 


    /// <summary>
    /// sent to main wnd with the file as parm whenever a file is played 
    /// </summary>
     IPC_PLAYING_FILE = 3003, 

    /// <summary>
    /// sent to main wnd with the file as parm whenever a file tag might be updated 
    /// </summary>
     IPC_FILE_TAG_MAY_HAVE_UPDATED = 3004, 



     IPC_ALLOW_PLAYTRACKING = 3007, 

    // send nonzero to allow, zero to disallow

     IPC_HOOK_OKTOQUIT = 3010, 


    /* 
    ** This is sent by Winamp asking if it's okay to close or not.
    **
    **   return 0 (zero) to prevent Winamp closing
    **   return anything else (ie 1) to allow Winamp to close
    **
    ** The better option is to let the message pass through to the
    ** original window proceedure since another plugin may want to
    ** have a say in the matter with regards to Winamp closing
    */


    /// <summary>
    /// pass 2 to write all, 1 to write playlist + common, 0 to write common+less common 
    /// </summary>
     IPC_WRITECONFIG = 3011, 


    // pass a string to be the name to register, and returns a value > 65536, which is a unique value you can use
    // for custom WM_WA_IPC messages. 
     IPC_REGISTER_WINAMP_IPCMESSAGE = 65536  
    }


    /**************************************************************************/

    /*
    ** Finally there are some WM_COMMAND messages that you can use to send 
    ** Winamp misc commands.
    ** 
    ** To send these, use:
    **
    ** SendMessage(hwnd_winamp, WM_COMMAND,command_name,0);
    **
    ** Edit: see wa_msgids.h for these now (DrO)
    */

#endif//_WA_IPC_H_

/*
** EOF.. Enjoy.
*/

    #region Winamp IPC structs (they might not work at this time)        

    //public struct windowCommand{
    //  int cmd;
    //  int x;
    //  int y;
    //  int align;
    //}; // send this as param to an IPC_PLCMD, IPC_MBCMD, IPC_VIDCMD


        
    //public struct transAccelStruct{
    //  IntPtr hwnd;
    //  int uMsg;
    //  int wParam;
    //  int lParam;
    //} ;

    //#if !NO_IVIDEO_DECLARE
    ////#ifdef __cplusplus //this is obviously  obsolete

    //class VideoOutput;
    //class SubsItem;

    //[MarshalAs(UnmanagedType.LPStruct)]
    [StructLayout(LayoutKind.Sequential)]
    public struct YV12_PLANE{
        byte/*unsigned char* */	baseAddr;
        long			rowBytes;
    };

    //public	struct YV12_PLANES{
    //    YV12_PLANE	y;
    //    YV12_PLANE	u;
    //    YV12_PLANE	v;
    //};

    //class IVideoOutput
    //{
    //  public:
    //    virtual ~IVideoOutput() { }
    //    virtual int open(int w, int h, int vflip, double aspectratio, unsigned int fmt)=0;
    //    virtual void setcallback(LRESULT (*msgcallback)(void *token, HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam), void *token) { }
    //    virtual void close()=0;
    //    virtual void draw(void *frame)=0;
    //    virtual void drawSubtitle(SubsItem *item) { }
    //    virtual void showStatusMsg(const char *text) { }
    //    virtual int get_latency() { return 0; }
    //    virtual void notifyBufferState(int bufferstate) { } /* 0-255*/

    //    virtual int extended(int param1, int param2, int param3) { return 0; } // Dispatchable, eat this!
    //};

    //class ITrackSelector 
    //{
    //  public:
    //    virtual int getNumAudioTracks()=0;
    //    virtual void enumAudioTrackName(int n, const char *buf, int size)=0;
    //    virtual int getCurAudioTrack()=0;
    //    virtual int getNumVideoTracks()=0;
    //    virtual void enumVideoTrackName(int n, const char *buf, int size)=0;
    //    virtual int getCurVideoTrack()=0;

    //    virtual void setAudioTrack(int n)=0;
    //    virtual void setVideoTrack(int n)=0;
    //};

    ////#endif //cplusplus
    //#endif//NO_IVIDEO_DECLARE

    //public struct waSpawnMenuParms
    //{
    //  IntPtr wnd;
    //  int xpos; // in screen coordinates
    //  int ypos;
    //};

    //// waSpawnMenuParms2 is used by the menubar submenus
    //public struct waSpawnMenuParms2
    //{
    //  IntPtr wnd;
    //  int xpos; // in screen coordinates
    //  int ypos;
    //  int width;
    //  int height;
    //} ;

    [StructLayout(LayoutKind.Sequential)]
    /*[MarshalAs(UnmanagedType.Struct)]*/
    public struct waSetPlColorsStruct
    {
      int numElems;
      /*int *elems;*//*IntPtr elems;*/
      [MarshalAs(UnmanagedType.LPArray)]
      int [] elems;
      /*HBITMAP bm; // set if you want to override*/

      /// <summary>
      /// Use Bitmap.GetHBitmap()
      /// </summary>
      IntPtr bitmap; 
    
    } ;

    //public struct converterEnumFmtStruct
    //{
    //  void (*enumProc)(int user_data, const char *desc, int fourcc);
    //  int user_data;
    //} ;
     

    /* (requires Winamp 2.92+)
    */


    //public struct burnCDStruct
    //{
    //  char cdletter;
    //  [MarshalAs(UnmanagedType.LPStr)]
    //  string playlist_file;
    //  IntPtr callback_hwnd;

    //  /// <summary>
    //  /// filled in by winamp.exe
    //  /// </summary>
    //  [MarshalAs(UnmanagedType.LPStr)]
    //  string error;
    //} ;
     
    /* (requires Winamp 5.0+)
    */

    [StructLayout(LayoutKind.Sequential)]
    public struct convertSetPriority
    {
      [MarshalAs(UnmanagedType.LPStruct)]
      convertFileStruct cfs;
      int priority;
    } ;
      

    [StructLayout(LayoutKind.Sequential)]
    public struct waHookTitleStruct
    {
      [MarshalAs(UnmanagedType.LPStr)]
      string filename;
      [MarshalAs(UnmanagedType.LPStr)]
      string title; // 2048 bytes
      int length;
      int force_useformatting; // can set this to 1 if you want to force a url to use title formatting
    } ;

    [StructLayout(LayoutKind.Sequential)]
    public struct convertConfigStruct
    {
      IntPtr hwndParent;
      int format;

      //filled in by winamp.exe
      IntPtr hwndConfig;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
      int [] extra_data;
    } ;

    [StructLayout(LayoutKind.Sequential)]
    public struct convertFileStruct
    {
      [MarshalAs(UnmanagedType.LPStr)]
      string sourcefile;  // "c:\\source.mp3"
      [MarshalAs(UnmanagedType.LPStr)]
      string destfile;    // "c:\\dest.pcm"
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
      int [] destformat; // like 'PCM ',srate,nch,bps
      IntPtr callbackhwnd; // window that will receive the IPC_CB_CONVERT notification messages
      
      /// <summary>
      /// filled in by winamp.exe
      /// </summary>\
      [MarshalAs(UnmanagedType.LPStr)]
      string error;        //if IPC_CONVERTFILE returns 0, the reason will be here

      int bytes_done;     //you can look at both of these values for speed statistics
      int bytes_total;
      int bytes_out;

      int killswitch;     // don't set it manually, use IPC_CONVERTFILE_END
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
      int [] extra_data; // for internal winamp use
    };

    //[StructLayout(LayoutKind.Sequential)]
    //public struct embedEnumStruct
    //{
    //  [MarshalAs(UnmanagedType.FunctionPtr)]
    //  delegate enumProc ?? 
    //  /*int (*enumProc)(embedWindowState *ws, struct embedEnumStruct *param); // return 1 to abort*/
    //  int user_data; // or more :)
    //} ;
    //  // pass 

    /*[StructLayout(LayoutKind.Sequential)]
    public struct embedWindowState
    {
      IntPtr me; //hwnd of the window

      int flags;

      /// <summary>
      /// WinAmpSDK.Win32.RECT , or just cast from SystemDrawingRectangle
      /// </summary>
      Win32.RECT r;

      //void *user_ptr; // for application use

      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] 
      int [] extra_data; // for internal winamp use
    };*/


    [StructLayout(LayoutKind.Sequential)]
    public struct _prefsDlgRec //prefsDlgRec
    {
      /// <summary>
      /// dll instance containing the dialog resource
      /// </summary>
      IntPtr hInst;   
      int dlgID;        // resource identifier of the dialog

      [UnmanagedFunctionPointer(CallingConvention.StdCall)]
      delegate void proc();
      //void *proc;       // window proceedure for handling the dialog defined as
                        // LRESULT CALLBACK PrefsPage(HWND,UINT,WPARAM,LPARAM)

      //char *name;       // name shown for the prefs page in the treelist
      [MarshalAs(UnmanagedType.LPStr)]
      string name;

      int where;        // section in the treelist the prefs page is to be added to
                        // 0 for General Preferences
                        // 1 for Plugins
                        // 2 for Skins
                        // 3 for Bookmarks (no longer in the 5.0+ prefs)
                        // 4 for Prefs     (appears to be the old 'Setup' section)

      int _id;          // for internal usage ??
      [MarshalAs(UnmanagedType.LPStruct)]
      IntPtr /*_prefsDlgRec*/ next;
    };

    //public struct waFormatTitle
    //{
    //  char *spec; // NULL=default winamp spec
    //  void *p;

    //  char *out;
    //  int out_len;

    //  char * (*TAGFUNC)(char * tag, void * p); //return 0 if not found
    //  void (*TAGFREEFUNC)(char * tag,void * p);
    //} ;


    //public struct wa_inflate_struct{
    //  int (*inflateReset)(void *strm);
    //  int (*inflateInit_)(void *strm,const char *version, int stream_size);
    //  int (*inflate)(void *strm, int flush);
    //  int (*inflateEnd)(void *strm);
    //  unsigned long (*crc32)(unsigned long crc, const unsigned  char *buf, unsigned int len);
    //};


    [StructLayout(LayoutKind.Sequential)]
    public struct infoBoxParam{
        IntPtr/*HWND*/ parent;
        [MarshalAs(UnmanagedType.LPStr)]
        string filename;
        //char *filename;
    } ;

    [StructLayout(LayoutKind.Sequential)]
    public struct extendedFileInfoStruct{
        [MarshalAs(UnmanagedType.LPStr)]
        string filename;
        //char *filename;
        [MarshalAs(UnmanagedType.LPStr)]
        string metadata;
        //char *metadata;
        [MarshalAs(UnmanagedType.LPStr)]
        string ret;
        //char *ret;
        int retlen;
    } ;

    [StructLayout(LayoutKind.Sequential)]
    public struct basicFileInfoStruct
    {
        [MarshalAs(UnmanagedType.LPStr)]
        string filename;
      //char *filename;

      int quickCheck; // set to 0 to always get, 1 for quick, 2 for default (if 2, quickCheck will be set to 0 if quick wasnot used)

      // filled in by winamp
      int length;
        [MarshalAs(UnmanagedType.LPStr)]
        string title;      
      //char *title;
      int titlelen;
    };

    /*public struct enqueueFileWithMetaStruct
    {
        [MarshalAs(UnmanagedType.LPStr)]
        string filename;
      //char *filename;
        [MarshalAs(UnmanagedType.LPStr)]
        string title;
      //char *title;
      int length;
    }; // send this to a IPC_PLAYFILE in a non WM_COPYDATA, 
    // and you get the nice desired result. if title is NULL, it is treated as a "thing",
    // otherwise it's assumed to be a file (for speed)*/

    #endregion


}