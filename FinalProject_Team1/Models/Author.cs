using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinalProject_Team1.Models
{
    public class Author
    {
        [Key]
        public int AuthorId { get; set; }
        
        [Required]
        [StringLength(100)]
        public required string Name { get; set; }
        
        [DataType(DataType.Date)]
        [Display(Name = "Birth Date")]
        public DateTime? BirthDate { get; set; }
        
        [DataType(DataType.Date)]
        [Display(Name = "Death Date")]
        public DateTime? DeathDate { get; set; }
        
        [StringLength(4000)]
        public string? Bio { get; set; }
        
        // Navigation property
        public virtual ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
    }
}
