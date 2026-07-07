using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("whatsappdb.appointment_bot")]
    public class AppointmentBotModel
    {
        [Key]
        public int bot_id { get; set; }

        public string? tenant_code { get; set; }

        public int? bh_code { get; set; }

        public string? whatsapp_api_url { get; set; }

        public string? whatsapp_phonenum_id { get; set; }

        public string? whatsapp_access_token { get; set; }
    }
}