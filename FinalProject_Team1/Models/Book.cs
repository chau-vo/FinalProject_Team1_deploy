using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinalProject_Team1.Models
{
    public class Book
    {
        [Key]
        public int BookId { get; set; }
        
        [Required]
        [StringLength(255)]
        public required string Title { get; set; }
        
        // ISBN10 property removed as it doesn't exist in the database
        
        [StringLength(13)]
        [Display(Name = "ISBN")]
        public string? ISBN13 { get; set; }
        
        [DataType(DataType.Date)]
        [Display(Name = "Publish Date")]
        public DateTime? PublishDate { get; set; }
        
        [Display(Name = "Number of Pages")]
        public int? NumberOfPages { get; set; }
        
        [StringLength(255)]
        [Display(Name = "Cover URL")]
        public string? CoverUrl { get; set; }
        
        [StringLength(4000)]
        public string? Description { get; set; }
        
        // Navigation properties
        public virtual ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
        public virtual ICollection<BookSubject> BookSubjects { get; set; } = new List<BookSubject>();
    }
}
