using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCardProtocolAPI.Command.Models
{
    public class CloseAlarmModel
    {
        public bool IllegalVerificationAlarm { get; set; }
        public bool PasswordAlarm { get; set; }

        public bool DoorMagneticAlarm { get; set; }

        public bool BlacklistAlarm { get; set; }

        public bool FireAlarm { get; set; }

        public bool OpenDoorTimeoutAlarm { get; set;}
        public bool AntiDisassemblyAlarm { get; set; }
    }
}
