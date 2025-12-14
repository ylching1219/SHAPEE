using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SHAPEE.Controllers
{
    [Area("Student")]
    public class SupportController : Controller
    {
        private readonly DB _context;

        public SupportController(DB context)
        {
            _context = context;
        }


        // STUDENT AREA


        // List tickets for current student
        public IActionResult TicketList()
        {
            var userId = User.Identity.Name;
            var tickets = _context.SupportTickets
                                  .Where(t => t.UserId == userId)
                                  .Include(t => t.TicketMessages)
                                  .ToList();
            return View(tickets);
        }

        // Create new ticket (GET)
        public IActionResult CreateTicket() => View();

        // Create new ticket (POST)
        [HttpPost]
        public IActionResult CreateTicket(SupportTicket model)
        {
            if (ModelState.IsValid)
            {
                model.Id = Guid.NewGuid().ToString();
                model.Status = "Open";
                model.CreatedAt = DateTime.Now;
                model.UserId = User.Identity.Name;

                _context.SupportTickets.Add(model);
                _context.SaveChanges();

                return RedirectToAction("TicketList");
            }
            return View(model);
        }

        // Ticket details with replies
        public IActionResult TicketDetails(string id)
        {
            var ticket = _context.SupportTickets
                                 .Include(t => t.TicketMessages)
                                 .ThenInclude(m => m.Sender)
                                 .FirstOrDefault(t => t.Id == id);
            if (ticket == null) return NotFound();
            return View(ticket);
        }


        // STAFF AREA

        [Area("Staff")]
        public IActionResult StaffTicketList()
        {
            var tickets = _context.SupportTickets
                                  .Include(t => t.TicketMessages)
                                  .ThenInclude(m => m.Sender)
                                  .ToList();
            return View(tickets);
        }

        [Area("Staff")]
        public IActionResult StaffTicketDetails(string id)
        {
            var ticket = _context.SupportTickets
                                 .Include(t => t.TicketMessages)
                                 .ThenInclude(m => m.Sender)
                                 .FirstOrDefault(t => t.Id == id);
            if (ticket == null) return NotFound();
            return View(ticket);
        }

        [HttpPost]
        [Area("Staff")]
        public IActionResult Reply(string ticketId, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return RedirectToAction("StaffTicketDetails", new { id = ticketId });

            // Fetch the ticket
            var ticket = _context.SupportTickets.Find(ticketId);
            if (ticket == null)
                return NotFound();

            // Prevent replying if ticket is resolved
            if (ticket.Status == "Resolved")
            {
                TempData["Error"] = "Cannot reply to a resolved ticket.";
                return RedirectToAction("StaffTicketDetails", new { id = ticketId });
            }

            // Create the reply
            var reply = new TicketMessage
            {
                Id = Guid.NewGuid().ToString(),
                SupportTicketId = ticketId,
                SenderId = User.Identity.Name, 
                Content = message,
                SentAt = DateTime.Now
            };

            _context.TicketMessages.Add(reply);
            _context.SaveChanges();

            return RedirectToAction("StaffTicketDetails", new { id = ticketId });
        }


        [Area("Staff")]
        public IActionResult CloseTicket(string id)
        {
            var ticket = _context.SupportTickets.Find(id);
            if (ticket != null)
            {
                ticket.Status = "Resolved";
                ticket.ResolvedAt = DateTime.Now;
                _context.SaveChanges();
            }
            return RedirectToAction("StaffTicketDetails", new { id });
        }
    }
}
