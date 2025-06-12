#pragma once

#define EXPORT_SPEC extern "C" __declspec(dllexport)

EXPORT_SPEC void* trigger_create(char* modelDir, char* dictionaryFile, bool verboseLogging);

EXPORT_SPEC int trigger_reconfigure(void* decoder, char* keywordFile);

EXPORT_SPEC int trigger_start_processing(void* decoder);

EXPORT_SPEC int trigger_stop_processing(void* decoder);

EXPORT_SPEC bool trigger_process_samples(void* decoder, short* samples, int numSamples);

EXPORT_SPEC bool trigger_get_in_speech(void* decoder);

EXPORT_SPEC void trigger_get_last_hyp(void* decoder, char* buffer);

EXPORT_SPEC int trigger_free(void* decoder);