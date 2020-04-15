using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Twilio.AspNet.Common;
using Twilio.TwiML;

namespace TwilioActors
{
    [Serializable]
    public class TwilioVoiceTakeOffHoldState
    {
        VoiceRequest _LastVoiceRequest;
        public VoiceRequest LastVoiceRequest
        {
            get => _LastVoiceRequest;
            set
            {                
                this.VoiceRequestLog.Add(value);
                _LastVoiceRequest = value;
            }
        }

        VoiceResponse _LastVoiceResponse;
        public VoiceResponse LastVoiceResponse
        {
            get => _LastVoiceResponse;
            set
            {
                this.VoiceResponseLog.Add(value);
                _LastVoiceResponse = value;
            }
        }

        public List<VoiceRequest> VoiceRequestLog { get; set; } = new List<VoiceRequest>();
        public List<VoiceResponse> VoiceResponseLog { get; set; } = new List<VoiceResponse>();
    }
}
