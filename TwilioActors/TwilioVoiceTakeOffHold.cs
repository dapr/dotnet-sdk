using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Dapr.Actors;
using Dapr.Actors.Runtime;
using Twilio.AspNet.Common;

namespace TwilioActors
{
    public class TwilioVoiceTakeOffHold : Actor //, IRemindable
    {
        public readonly string StateName = nameof(TwilioVoiceTakeOffHoldState);
        public TwilioVoiceTakeOffHold(ActorService service, ActorId actorId)
               : base(service, actorId)
        {

        }


        private Task<bool> TakeCallOffHold()
        {
            var result = false;
            ActorState.SetCurrentStep(VoiceRequestActorState.WorkflowPosition.TakeCallOffHold);

            CallResource call = null;
            try
            {
                Console.WriteLine("TakeCalloffHold - Attempting to take the call off hold, and start call work flow");
                //we are telling twilio to do this web hook, and break into the call that is currently on hold
                var voiceResponse = new VoiceResponse();
                voiceResponse.Pause(2).Say("Hello Caller - I'm Twilio Voice Actor - How Can I help you?")
                  .Pause(2)
                  .Say("Good-bye - test was a success!")
                  .Pause(3)
                  .Hangup();

                call = CallResource.Update(
                  twiml: voiceResponse.ToString(),
                  url: new Uri(" https://twilioringing.ngrok.io/VoiceRequestProxy/GreetCaller", UriKind.Absolute), //jumps to VoiceRequestProxy method "GreetCaller"
                                                                                                                   //pathSid: ActorState.LastTwilioRequestReponse.VoiceRequest.CallSid //Actor ID is the CallSid ID
                  pathSid: ActorState.ActorID
                );
            }
        }
     

        public async Task SaveVoiceRequestData(VoiceRequest voiceRequest)
        {
            Console.WriteLine($"This is Actor id {this.Id} with data {voiceRequest}.");

            // Set State using StateManager, state is saved after the method execution.
            await this.StateManager.SetStateAsync<TwilioVoiceTakeOffHoldState>(StateName, new TwilioVoiceTakeOffHoldState());
        }

        /// <summary>
        /// Application method to return state of this actor via a syncronise property
        /// </summary>
        private VoiceRequestActorState ActorState
        {
            get
            {
                if (_actorState == default)
                {
                    Console.WriteLine("ActorState should have been initialized via the constructor");
                    throw new Exception("ActorState cannot be null");
                }
                return _actorState;
            }
            set
            {
                if (_actorState == default & value != default)
                    _actorState = value;
                else
                {
                    Console.WriteLine("Throwing exception due to setting of ActorState is invalid");
                    throw new ArgumentException("_ActorState should only be set once, and hence is must be null in order to set it");
                }
            }
        }

    }
}
