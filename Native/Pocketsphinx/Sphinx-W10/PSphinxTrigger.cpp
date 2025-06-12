/**
* Created by Toine de Boer, Enbyin (NL)
*
* intended as kick-start using PocketSphinx on Windows mobile platforms
*/

#include <collection.h>
#include <algorithm>
using namespace Platform::Collections;
using namespace Windows::Foundation::Collections;

#include "PSphinxTrigger.h"

using namespace Sphinx_W10;
using namespace Platform;

#include <pocketsphinx.h>
#include <sphinxbase/err.h>
#include <sphinxbase/jsgf.h>

#include "Output.h"

#pragma region Private Fields

char applicationInstallFolderPath[1024];
char applicationLocalStorageFolder[1024];

#pragma endregion

#pragma region Constructor

PSphinxTrigger::PSphinxTrigger()
{
}

#pragma endregion

#pragma region Static Helpers

// Dont forget to free memory after usage "result" output
static char* concat(char *s1, char *s2)
{
	char *result = (char *)malloc(strlen(s1) + strlen(s2) + 1);
	strcpy(result, s1);
	strcat(result, s2);
	return result;
}

// Dont forget to free memory after usage "characters" output
static char* convertStringToChars(Platform::String^ input)
{
	const wchar_t *platformCharacters = input->Data();

	int Size = wcslen(platformCharacters);
	char *characters = new char[Size + 1];
	characters[Size] = 0;
	for (int i = 0; i < Size; i++)
	{
		characters[i] = (char)platformCharacters[i];
	}

	return characters;
}

String^ convertCharsToString(const char* chars)
{
	if (chars == NULL)
	{
		return ref new Platform::String();
	}

	static wchar_t buffer[1024];
	mbstowcs(buffer, chars, 1024);
	return ref new Platform::String(buffer);
}

#pragma endregion

Platform::Boolean PSphinxTrigger::trigger_create(Platform::String^ modelDir, Platform::String^ dictionaryFile, Platform::Boolean verboseLogging)
{
	Output::WriteLine("creating sphinx recognizer");

	// Get Local Storage Path
	wcstombs(applicationLocalStorageFolder, Windows::Storage::ApplicationData::Current->LocalFolder->Path->Data(), 1024);
	// Get Installed Folder path
	wcstombs(applicationInstallFolderPath, Windows::ApplicationModel::Package::Current->InstalledLocation->Path->Data(), 1024);

	// Create full hmm and dict file paths
	auto ChmmFilePath = convertStringToChars(modelDir);
	auto CdictFilePath = convertStringToChars(dictionaryFile);
	char *hmmPath = concat(applicationInstallFolderPath, ChmmFilePath);
	char *dictPath = concat(applicationInstallFolderPath, CdictFilePath);

	cmd_ln_t* config = NULL;

	if (verboseLogging)
	{
		config = cmd_ln_init(NULL, ps_args(), true,
			"-hmm", hmmPath,
			"-dict", dictPath,
			"-mmap", "no",
			NULL);
	}
	else
	{
		config = cmd_ln_init(NULL, ps_args(), true,
			"-hmm", hmmPath,
			"-dict", dictPath,
			//"-logfn", "NUL",
			"-mmap", "no",
			NULL);
	}

	if (config == NULL)
	{
		Output::WriteLine("Could not create a config");
		return false;
	}

	ps = ps_init(config);

	if (ps == NULL)
	{
		Output::WriteLine("Could not create a decoder");
		return false;
	}

	cmd_ln_free_r(config);

	user_is_speaking = false;
	last_hyp = new char[512];
	last_hyp[0] = 0;

	// Cleanup
	free(ChmmFilePath);
	free(CdictFilePath);
	free(hmmPath);
	free(dictPath);

	return true;
}

Platform::Boolean PSphinxTrigger::trigger_reconfigure(Platform::String^ keyfile)
{
	Output::WriteLine("reconfiguring sphinx");

	char* keyfileC = convertStringToChars(keyfile);

	if (ps_set_kws(ps, "keyword_search", keyfileC) != 0)
	{
		free(keyfileC);
		Output::WriteLine("could not create kws_search struct");
		return false;
	}

	free(keyfileC);

	if (ps_set_search(ps, "keyword_search") != 0)
	{
		Output::WriteLine("could not set active ps search");
		return false;
	}

	return true;
}

Platform::Boolean PSphinxTrigger::trigger_start_processing()
{
	Output::WriteLine("sphinx process start");
	utt_started = true;
	return (ps_start_utt(ps) == 0); // todo use ps_start_stream?
}

Platform::Boolean PSphinxTrigger::trigger_stop_processing()
{
	Output::WriteLine("sphinx process stop");
	if (utt_started)
	{
		utt_started = false;
		return (ps_end_utt(ps) == 0);
	}

	return true;
}

Platform::Boolean PSphinxTrigger::trigger_process_samples(const Platform::Array<int16>^ samples, int numSamples)
{
	ps_process_raw(ps, samples->Data, numSamples, false, false);
	uint8 in_speech = ps_get_in_speech(ps);
	if (in_speech && !user_is_speaking)
	{
		user_is_speaking = true;
	}

	bool returnVal = false;

	int score;
	const char* hyp = ps_get_hyp(ps, &score);

	if (hyp)
	{
		//printf("            tenative hyp %s\n", hyp);
		if (!triggered)
		{
			returnVal = true;
			triggered = true;
			size_t hypsize = strnlen(hyp, 500);
			strncpy(last_hyp, hyp, hypsize);
			last_hyp[hypsize] = 0;
			//printf("            adapter last hyp is %s\n", hyp);
		}
	}

	if (!in_speech && user_is_speaking)
	{
		/* speech -> silence transition, time to start new utterance  */
		ps_end_utt(ps);
		utt_started = false;

		hyp = ps_get_hyp(ps, &score);

		if (hyp)
		{
			//printf("            final hyp %s\n", hyp);
			if (!triggered)
			{
				returnVal = true;
				triggered = true;
				size_t hypsize = strnlen(hyp, 500);
				strncpy(last_hyp, hyp, hypsize);
				last_hyp[hypsize] = 0;
				//printf("            adapter last hyp is %s\n", hyp);
			}
		}

		if (ps_start_utt(ps) != 0)
		{
			Output::WriteLine("failed to restart utterance");
		}
		else
		{
			utt_started = true;
		}

		user_is_speaking = false;
		triggered = false;
	}

	return returnVal;
}

Platform::String^ PSphinxTrigger::trigger_get_last_hyp()
{
	return convertCharsToString(last_hyp);
}

Platform::Boolean PSphinxTrigger::trigger_free()
{
	ps_free(ps);
	free(last_hyp);
	return true;
}


