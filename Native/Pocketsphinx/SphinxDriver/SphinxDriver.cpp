// SphinxDriver.cpp : Defines the entry point for the console application.
//

#include "stdafx.h"
#include "psphinx_trigger.h"

int main()
{
	char* modelDir = "C:\\Users\\LOSTROMB\\Documents\\Visual Studio 2015\\Projects\\Durandal\\Data\\sphinx\\en-us-semi";
	char* dictFile = "C:\\Users\\LOSTROMB\\Documents\\Visual Studio 2015\\Projects\\Durandal\\Data\\sphinx\\cmudict_SPHINX_40.txt";
	char* keywordDef = "DURANDAL/1e-20/\nSTOP THE TIMER/1e-20/\nTRAILBLAZER/1e-20/\n";
	void* decoder = trigger_create(modelDir, dictFile, true);
	trigger_reconfigure(decoder, keywordDef);
	trigger_start_processing(decoder);
	return 0;
}

