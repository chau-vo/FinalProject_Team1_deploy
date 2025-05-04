using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinalProject_Team1.Models
{
    public class BookAuthor
    {
        [Key]
        [Column(Order = 1)]
        public int BookId { get; set; }
        
        [Key]
        [Column(Order = 2)]
        public int AuthorId { get; set; }
        
        // Navigation properties
        [ForeignKey("BookId")]
        public virtual Book? Book { get; set; }
        
        [ForeignKey("AuthorId")]
        public virtual Author? Author { get; set; }
    }
}
