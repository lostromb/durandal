using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DialogTests
{
    using Durandal.API;
    using Durandal.Common.Dialog;

    public class TestAnswerProvider : IAnswerProvider
    {
        public BasicAnswer BasicAnswer = new BasicAnswer();
        public BasicTreeAnswer BasicTreeAnswer = new BasicTreeAnswer();
        public SideSpeechAnswer SideSpeechAnswer = new SideSpeechAnswer();
        public RetryTreeAnswer RetryTreeAnswer = new RetryTreeAnswer();
        public ClientCapsAnswer ClientCapsAnswer = new ClientCapsAnswer();
        public SandboxAnswer SandboxAnswer = new SandboxAnswer();
        public CrossDomainAnswer CrossDomainAnswerA = new CrossDomainAnswer("crossdomain_a");
        public CrossDomainAnswer CrossDomainAnswerB = new CrossDomainAnswer("crossdomain_b");
        public CrossDomainSuperAnswer CrossDomainSuper = new CrossDomainSuperAnswer();
        public CrossDomainSubAnswer CrossDomainSub = new CrossDomainSubAnswer();
        public TriggerAnswer TriggerAnswerA = new TriggerAnswer("trigger_a");
        public TriggerAnswer TriggerAnswerB = new TriggerAnswer("trigger_b");
        public ReflectionAnswer ReflectionAnswer = new ReflectionAnswer();

        public Answer GetAnswerForDomain(string domainName)
        {
            switch (domainName)
            {
                case "basic":
                    return BasicAnswer;
                case "basictree":
                    return BasicTreeAnswer;
                case "side_speech":
                    return SideSpeechAnswer;
                case "retrytree":
                    return RetryTreeAnswer;
                case "clientcaps":
                    return ClientCapsAnswer;
                case "sandbox":
                    return SandboxAnswer;
                case "crossdomain_a":
                    return CrossDomainAnswerA;
                case "crossdomain_b":
                    return CrossDomainAnswerB;
                case "cd_super":
                    return CrossDomainSuper;
                case "cd_sub":
                    return CrossDomainSub;
                case "trigger_a":
                    return TriggerAnswerA;
                case "trigger_b":
                    return TriggerAnswerB;
                case "reflection":
                    return ReflectionAnswer;
            }
            return null;
        }

        public void ResetAllPlugins()
        {
            BasicAnswer.Reset();
            BasicTreeAnswer.Reset();
            SideSpeechAnswer.Reset();
            RetryTreeAnswer.Reset();
        }
    }
}
