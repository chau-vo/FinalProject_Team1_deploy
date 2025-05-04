using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinalProject_Team1.Models
{
    public class BookSubject
    {
        [Key]
        [Column(Order = 1)]
        public int BookId { get; set; }
        
        [Key]
        [Column(Order = 2)]
        public int SubjectId { get; set; }
        
        // Navigation properties
        [ForeignKey("BookId")]
        public virtual Book? Book { get; set; }
        
        [ForeignKey("SubjectId")]
        public virtual Subject? Subject { get; set; }
    }
}
