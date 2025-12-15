//using System;
//using System.ComponentModel.DataAnnotations;
//using System.ComponentModel.DataAnnotations.Schema;

//namespace SHAPEE.Models
//{
//    public class TicketReply
//    {
//        [Key]
//        public int ReplyId { get; set; }

//        [Required]
//        public int TicketId { get; set; }

//        [ForeignKey("TicketId")]
//        public SupportTicket Ticket { get; set; }

//        [Required]
//        public string ResponderId { get; set; } // Staff user Id

//        [Required]
//        public string Message { get; set; }

//        public DateTime RepliedAt { get; set; } = DateTime.Now;
//    }
//}
