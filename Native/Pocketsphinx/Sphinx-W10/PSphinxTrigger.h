#pragma once

#include <pocketsphinx.h>

namespace Sphinx_W10
{
	public ref class PSphinxTrigger sealed
    {
    public:
		PSphinxTrigger();

		Platform::Boolean PSphinxTrigger::trigger_create(Platform::String^ modelDir, Platform::String^ dictionaryFile, Platform::Boolean verboseLogging);
		Platform::Boolean PSphinxTrigger::trigger_reconfigure(Platform::String^ keyfile);
		Platform::Boolean PSphinxTrigger::trigger_start_processing();
		Platform::Boolean PSphinxTrigger::trigger_stop_processing();
		Platform::Boolean PSphinxTrigger::trigger_process_samples(const Platform::Array<int16>^ samples, int numSamples);
		Platform::String^ PSphinxTrigger::trigger_get_last_hyp();
		Platform::Boolean PSphinxTrigger::trigger_free();

	private:
		ps_decoder_t* PSphinxTrigger::ps;
		bool PSphinxTrigger::utt_started ;
		bool PSphinxTrigger::user_is_speaking;
		bool PSphinxTrigger::triggered;
		char* PSphinxTrigger::last_hyp;
    };
}