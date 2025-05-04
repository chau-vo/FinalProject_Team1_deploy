using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FinalProject_Team1.Models
{
    public class Subject
    {
        [Key]
        public int SubjectId { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        
        // Navigation property
        public virtual ICollection<BookSubject> BookSubjects { get; set; } = new List<BookSubject>();
    }
}
