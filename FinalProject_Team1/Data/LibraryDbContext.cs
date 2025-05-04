using FinalProject_Team1.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_Team1.Data
{
    public class LibraryDbContext : DbContext
    {
        public LibraryDbContext(DbContextOptions<LibraryDbContext> options)
            : base(options)
        {
        }

        public DbSet<Author> Authors { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<BookAuthor> BookAuthors { get; set; }
        public DbSet<BookSubject> BookSubjects { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Set table names to match the database schema
            modelBuilder.Entity<Author>().ToTable("author");
            modelBuilder.Entity<Book>().ToTable("book");
            modelBuilder.Entity<Subject>().ToTable("subject");
            modelBuilder.Entity<BookAuthor>().ToTable("book_author");
            modelBuilder.Entity<BookSubject>().ToTable("book_subject");

            // Configure column names for Author
            modelBuilder.Entity<Author>()
                .Property(a => a.AuthorId).HasColumnName("author_id");
            modelBuilder.Entity<Author>()
                .Property(a => a.Name).HasColumnName("name");
            modelBuilder.Entity<Author>()
                .Property(a => a.BirthDate).HasColumnName("birth_date");
            modelBuilder.Entity<Author>()
                .Property(a => a.DeathDate).HasColumnName("death_date");
            modelBuilder.Entity<Author>()
                .Property(a => a.Bio).HasColumnName("bio");

            // Configure column names for Book
            modelBuilder.Entity<Book>()
                .Property(b => b.BookId).HasColumnName("book_id");
            modelBuilder.Entity<Book>()
                .Property(b => b.Title).HasColumnName("title");
            // Comment out ISBN mapping since it doesn't exist in the database
            // modelBuilder.Entity<Book>()
            //     .Property(b => b.ISBN10).HasColumnName("isbn");
            modelBuilder.Entity<Book>()
                .Property(b => b.ISBN13).HasColumnName("isbn_13");
            modelBuilder.Entity<Book>()
                .Property(b => b.PublishDate).HasColumnName("publish_date");
            modelBuilder.Entity<Book>()
                .Property(b => b.NumberOfPages).HasColumnName("number_of_pages");
            modelBuilder.Entity<Book>()
                .Property(b => b.CoverUrl).HasColumnName("cover_url");
            modelBuilder.Entity<Book>()
                .Property(b => b.Description).HasColumnName("description");

            // Configure column names for Subject
            modelBuilder.Entity<Subject>()
                .Property(s => s.SubjectId).HasColumnName("subject_id");
            modelBuilder.Entity<Subject>()
                .Property(s => s.Name).HasColumnName("name");

            // Configure column names for BookAuthor
            modelBuilder.Entity<BookAuthor>()
                .Property(ba => ba.BookId).HasColumnName("book_id");
            modelBuilder.Entity<BookAuthor>()
                .Property(ba => ba.AuthorId).HasColumnName("author_id");

            // Configure column names for BookSubject
            modelBuilder.Entity<BookSubject>()
                .Property(bs => bs.BookId).HasColumnName("book_id");
            modelBuilder.Entity<BookSubject>()
                .Property(bs => bs.SubjectId).HasColumnName("subject_id");

            // Configure composite keys for junction tables
            modelBuilder.Entity<BookAuthor>()
                .HasKey(ba => new { ba.BookId, ba.AuthorId });

            modelBuilder.Entity<BookSubject>()
                .HasKey(bs => new { bs.BookId, bs.SubjectId });

            // Configure relationships
            modelBuilder.Entity<BookAuthor>()
                .HasOne(ba => ba.Book)
                .WithMany(b => b.BookAuthors)
                .HasForeignKey(ba => ba.BookId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BookAuthor>()
                .HasOne(ba => ba.Author)
                .WithMany(a => a.BookAuthors)
                .HasForeignKey(ba => ba.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BookSubject>()
                .HasOne(bs => bs.Book)
                .WithMany(b => b.BookSubjects)
                .HasForeignKey(bs => bs.BookId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BookSubject>()
                .HasOne(bs => bs.Subject)
                .WithMany(s => s.BookSubjects)
                .HasForeignKey(bs => bs.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
