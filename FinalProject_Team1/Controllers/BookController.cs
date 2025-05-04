using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FinalProject_Team1.Data;
using FinalProject_Team1.Models;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace FinalProject_Team1.Controllers
{
    public class ExploreController : Controller
    {
        private readonly LibraryDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ExploreController> _logger;

        public ExploreController(LibraryDbContext context, IHttpClientFactory httpClientFactory, ILogger<ExploreController> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            
            // Set a longer command timeout globally
            _context.Database.SetCommandTimeout(180); // 3 minutes timeout
            
            // Warm up the database connection
            try
            {
                // This will be executed asynchronously to avoid blocking the constructor
                Task.Run(async () => {
                    try {
                        await _context.Database.CanConnectAsync();
                    } catch (Exception ex) {
                        _logger.LogError(ex, "Error warming up database connection");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing database connection");
            }
        }

        // Helper method to execute database operations with retry logic
        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3)
        {
            int retryCount = 0;
            while (true)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex)
                {
                    retryCount++;
                    if (retryCount > maxRetries)
                    {
                        _logger.LogError(ex, $"Failed after {maxRetries} retries");
                        throw;
                    }
                    
                    _logger.LogWarning(ex, $"Database operation failed, retrying ({retryCount}/{maxRetries})...");
                    await Task.Delay(500 * retryCount); // Exponential backoff
                }
            }
        }
        
        // GET: Explore
        public async Task<IActionResult> Index()
        {
            try
            {
                List<Book> books = new List<Book>();
                
                await ExecuteWithRetryAsync(async () => {
                    using (var connection = _context.Database.GetDbConnection())
                    {
                        await connection.OpenAsync();
                        
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = @"
                                SELECT b.book_id, b.title, b.isbn_13, b.publish_date, b.number_of_pages, b.cover_url, b.description
                                FROM book b
                                ORDER BY b.title";
                            
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    books.Add(new Book
                                    {
                                        BookId = reader.GetInt32(0),
                                        Title = reader.GetString(1),
                                        ISBN13 = !reader.IsDBNull(2) ? reader.GetString(2) : null,
                                        PublishDate = !reader.IsDBNull(3) ? reader.GetDateTime(3) : null,
                                        NumberOfPages = !reader.IsDBNull(4) ? reader.GetInt32(4) : null,
                                        CoverUrl = !reader.IsDBNull(5) ? reader.GetString(5) : null,
                                        Description = !reader.IsDBNull(6) ? reader.GetString(6) : null
                                    });
                                }
                            }
                        }
                    }
                    return true;
                });
                
                return View(books);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Explore Index action");
                ViewBag.DatabaseError = "Could not retrieve books from the database. Error: " + ex.Message;
                return View(new List<Book>());
            }
        }

        // GET: Explore/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                Book book = null;
                
                await ExecuteWithRetryAsync(async () => {
                    using (var connection = _context.Database.GetDbConnection())
                    {
                        await connection.OpenAsync();
                        
                        // Get book details
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = @"
                                SELECT b.book_id, b.title, b.isbn_13, b.publish_date, b.number_of_pages, b.cover_url, b.description
                                FROM book b
                                WHERE b.book_id = @bookId";
                            
                            var parameter = command.CreateParameter();
                            parameter.ParameterName = "@bookId";
                            parameter.Value = id;
                            command.Parameters.Add(parameter);
                            
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    book = new Book
                                    {
                                        BookId = reader.GetInt32(0),
                                        Title = reader.GetString(1),
                                        ISBN13 = !reader.IsDBNull(2) ? reader.GetString(2) : null,
                                        PublishDate = !reader.IsDBNull(3) ? reader.GetDateTime(3) : null,
                                        NumberOfPages = !reader.IsDBNull(4) ? reader.GetInt32(4) : null,
                                        CoverUrl = !reader.IsDBNull(5) ? reader.GetString(5) : null,
                                        Description = !reader.IsDBNull(6) ? reader.GetString(6) : null,
                                        BookAuthors = new List<BookAuthor>(),
                                        BookSubjects = new List<BookSubject>()
                                    };
                                }
                            }
                        }
                        
                        if (book == null)
                        {
                            return false;
                        }
                        
                        // Get authors for the book
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = @"
                                SELECT a.author_id, a.name
                                FROM author a
                                JOIN book_author ba ON a.author_id = ba.author_id
                                WHERE ba.book_id = @bookId
                                ORDER BY a.name";
                            
                            var parameter = command.CreateParameter();
                            parameter.ParameterName = "@bookId";
                            parameter.Value = id;
                            command.Parameters.Add(parameter);
                            
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    book.BookAuthors.Add(new BookAuthor
                                    {
                                        BookId = book.BookId,
                                        AuthorId = reader.GetInt32(0),
                                        Author = new Author
                                        {
                                            AuthorId = reader.GetInt32(0),
                                            Name = reader.GetString(1)
                                        }
                                    });
                                }
                            }
                        }
                        
                        // Get subjects for the book
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = @"
                                SELECT s.subject_id, s.name
                                FROM subject s
                                JOIN book_subject bs ON s.subject_id = bs.subject_id
                                WHERE bs.book_id = @bookId
                                ORDER BY s.name";
                            
                            var parameter = command.CreateParameter();
                            parameter.ParameterName = "@bookId";
                            parameter.Value = id;
                            command.Parameters.Add(parameter);
                            
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    book.BookSubjects.Add(new BookSubject
                                    {
                                        BookId = book.BookId,
                                        SubjectId = reader.GetInt32(0),
                                        Subject = new Subject
                                        {
                                            SubjectId = reader.GetInt32(0),
                                            Name = reader.GetString(1)
                                        }
                                    });
                                }
                            }
                        }
                    }
                    return true;
                });
                
                if (book == null)
                {
                    return NotFound();
                }
                
                return View(book);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Explore Details action");
                ViewBag.DatabaseError = "Could not retrieve book details. Error: " + ex.Message;
                return View(new Book { BookId = id.Value, Title = "Error Loading Book" });
            }
        }

        // GET: Book/Create
        public IActionResult Create()
        {
            ViewData["Authors"] = new SelectList(_context.Authors, "AuthorId", "Name");
            ViewData["Subjects"] = new SelectList(_context.Subjects, "SubjectId", "Name");
            return View();
        }

        // POST: Book/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("BookId,Title,ISBN13,PublishDate,NumberOfPages,CoverUrl,Description")] Book book, int[] selectedAuthors, int[] selectedSubjects)
        {
            if (ModelState.IsValid)
            {
                _context.Add(book);
                await _context.SaveChangesAsync();

                // Add selected authors
                if (selectedAuthors != null)
                {
                    foreach (var authorId in selectedAuthors)
                    {
                        _context.BookAuthors.Add(new BookAuthor
                        {
                            BookId = book.BookId,
                            AuthorId = authorId
                        });
                    }
                }

                // Add selected subjects
                if (selectedSubjects != null)
                {
                    foreach (var subjectId in selectedSubjects)
                    {
                        _context.BookSubjects.Add(new BookSubject
                        {
                            BookId = book.BookId,
                            SubjectId = subjectId
                        });
                    }
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewData["Authors"] = new SelectList(_context.Authors, "AuthorId", "Name");
            ViewData["Subjects"] = new SelectList(_context.Subjects, "SubjectId", "Name");
            return View(book);
        }

        // GET: Book/Edit/5
        public IActionResult Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                Book book = null;
                List<Author> authors = new List<Author>();
                List<Subject> subjects = new List<Subject>();
                List<int> selectedAuthors = new List<int>();
                List<int> selectedSubjects = new List<int>();
                
                using (var connection = _context.Database.GetDbConnection())
                {
                    connection.Open();
                    
                    // Get book details
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                            SELECT b.book_id, b.title, b.isbn_13, b.publish_date, b.number_of_pages, b.cover_url, b.description
                            FROM book b
                            WHERE b.book_id = @bookId";
                        
                        var parameter = command.CreateParameter();
                        parameter.ParameterName = "@bookId";
                        parameter.Value = id;
                        command.Parameters.Add(parameter);
                        
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                book = new Book
                                {
                                    BookId = reader.GetInt32(0),
                                    Title = reader.GetString(1),
                                    ISBN13 = !reader.IsDBNull(2) ? reader.GetString(2) : null,
                                    PublishDate = !reader.IsDBNull(3) ? reader.GetDateTime(3) : null,
                                    NumberOfPages = !reader.IsDBNull(4) ? reader.GetInt32(4) : null,
                                    CoverUrl = !reader.IsDBNull(5) ? reader.GetString(5) : null,
                                    Description = !reader.IsDBNull(6) ? reader.GetString(6) : null,
                                    BookAuthors = new List<BookAuthor>(),
                                    BookSubjects = new List<BookSubject>()
                                };
                            }
                        }
                    }
                    
                    if (book == null)
                    {
                        return NotFound();
                    }
                    
                    // Get all authors
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT author_id, name FROM author ORDER BY name";
                        
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                authors.Add(new Author
                                {
                                    AuthorId = reader.GetInt32(0),
                                    Name = reader.GetString(1)
                                });
                            }
                        }
                    }
                    
                    // Get all subjects
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT subject_id, name FROM subject ORDER BY name";
                        
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                subjects.Add(new Subject
                                {
                                    SubjectId = reader.GetInt32(0),
                                    Name = reader.GetString(1)
                                });
                            }
                        }
                    }
                    
                    // Get selected authors for this book
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT author_id FROM book_author WHERE book_id = @bookId";
                        
                        var parameter = command.CreateParameter();
                        parameter.ParameterName = "@bookId";
                        parameter.Value = id;
                        command.Parameters.Add(parameter);
                        
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                selectedAuthors.Add(reader.GetInt32(0));
                            }
                        }
                    }
                    
                    // Get selected subjects for this book
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT subject_id FROM book_subject WHERE book_id = @bookId";
                        
                        var parameter = command.CreateParameter();
                        parameter.ParameterName = "@bookId";
                        parameter.Value = id;
                        command.Parameters.Add(parameter);
                        
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                selectedSubjects.Add(reader.GetInt32(0));
                            }
                        }
                    }
                }
                
                ViewBag.Authors = new SelectList(authors, "AuthorId", "Name");
                ViewBag.Subjects = new SelectList(subjects, "SubjectId", "Name");
                ViewBag.SelectedAuthors = selectedAuthors;
                ViewBag.SelectedSubjects = selectedSubjects;
                
                return View(book);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Explore Edit action");
                ViewBag.DatabaseError = "Could not retrieve book details for editing. Error: " + ex.Message;
                return View(new Book { BookId = id.Value, Title = "Error Loading Book" });
            }
        }

        // POST: Explore/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, [Bind("BookId,Title,ISBN13,PublishDate,NumberOfPages,CoverUrl,Description")] Book book, int[] selectedAuthors, int[] selectedSubjects)
        {
            if (id != book.BookId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    using (var connection = _context.Database.GetDbConnection())
                    {
                        connection.Open();
                        using (var transaction = connection.BeginTransaction())
                        {
                            try
                            {
                                // Update book details
                                using (var command = connection.CreateCommand())
                                {
                                    command.Transaction = transaction;
                                    command.CommandText = @"
                                        UPDATE book 
                                        SET title = @title, 
                                            isbn_13 = @isbn13, 
                                            publish_date = @publishDate, 
                                            number_of_pages = @numberOfPages, 
                                            cover_url = @coverUrl, 
                                            description = @description
                                        WHERE book_id = @bookId";
                                    
                                    var pBookId = command.CreateParameter();
                                    pBookId.ParameterName = "@bookId";
                                    pBookId.Value = book.BookId;
                                    command.Parameters.Add(pBookId);
                                    
                                    var pTitle = command.CreateParameter();
                                    pTitle.ParameterName = "@title";
                                    pTitle.Value = book.Title;
                                    command.Parameters.Add(pTitle);
                                    
                                    var pIsbn13 = command.CreateParameter();
                                    pIsbn13.ParameterName = "@isbn13";
                                    pIsbn13.Value = book.ISBN13 != null ? (object)book.ISBN13 : DBNull.Value;
                                    command.Parameters.Add(pIsbn13);
                                    
                                    var pPublishDate = command.CreateParameter();
                                    pPublishDate.ParameterName = "@publishDate";
                                    pPublishDate.Value = book.PublishDate.HasValue ? (object)book.PublishDate.Value : DBNull.Value;
                                    command.Parameters.Add(pPublishDate);
                                    
                                    var pNumberOfPages = command.CreateParameter();
                                    pNumberOfPages.ParameterName = "@numberOfPages";
                                    pNumberOfPages.Value = book.NumberOfPages.HasValue ? (object)book.NumberOfPages.Value : DBNull.Value;
                                    command.Parameters.Add(pNumberOfPages);
                                    
                                    var pCoverUrl = command.CreateParameter();
                                    pCoverUrl.ParameterName = "@coverUrl";
                                    pCoverUrl.Value = book.CoverUrl != null ? (object)book.CoverUrl : DBNull.Value;
                                    command.Parameters.Add(pCoverUrl);
                                    
                                    var pDescription = command.CreateParameter();
                                    pDescription.ParameterName = "@description";
                                    pDescription.Value = book.Description != null ? (object)book.Description : DBNull.Value;
                                    command.Parameters.Add(pDescription);
                                    
                                    command.ExecuteNonQuery();
                                }
                                
                                // Delete existing author associations
                                using (var command = connection.CreateCommand())
                                {
                                    command.Transaction = transaction;
                                    command.CommandText = "DELETE FROM book_author WHERE book_id = @bookId";
                                    
                                    var parameter = command.CreateParameter();
                                    parameter.ParameterName = "@bookId";
                                    parameter.Value = book.BookId;
                                    command.Parameters.Add(parameter);
                                    
                                    command.ExecuteNonQuery();
                                }
                                
                                // Add new author associations
                                if (selectedAuthors != null && selectedAuthors.Length > 0)
                                {
                                    foreach (var authorId in selectedAuthors)
                                    {
                                        using (var command = connection.CreateCommand())
                                        {
                                            command.Transaction = transaction;
                                            command.CommandText = "INSERT INTO book_author (book_id, author_id) VALUES (@bookId, @authorId)";
                                            
                                            var pBookId = command.CreateParameter();
                                            pBookId.ParameterName = "@bookId";
                                            pBookId.Value = book.BookId;
                                            command.Parameters.Add(pBookId);
                                            
                                            var pAuthorId = command.CreateParameter();
                                            pAuthorId.ParameterName = "@authorId";
                                            pAuthorId.Value = authorId;
                                            command.Parameters.Add(pAuthorId);
                                            
                                            command.ExecuteNonQuery();
                                        }
                                    }
                                }
                                
                                // Delete existing subject associations
                                using (var command = connection.CreateCommand())
                                {
                                    command.Transaction = transaction;
                                    command.CommandText = "DELETE FROM book_subject WHERE book_id = @bookId";
                                    
                                    var parameter = command.CreateParameter();
                                    parameter.ParameterName = "@bookId";
                                    parameter.Value = book.BookId;
                                    command.Parameters.Add(parameter);
                                    
                                    command.ExecuteNonQuery();
                                }
                                
                                // Add new subject associations
                                if (selectedSubjects != null && selectedSubjects.Length > 0)
                                {
                                    foreach (var subjectId in selectedSubjects)
                                    {
                                        using (var command = connection.CreateCommand())
                                        {
                                            command.Transaction = transaction;
                                            command.CommandText = "INSERT INTO book_subject (book_id, subject_id) VALUES (@bookId, @subjectId)";
                                            
                                            var pBookId = command.CreateParameter();
                                            pBookId.ParameterName = "@bookId";
                                            pBookId.Value = book.BookId;
                                            command.Parameters.Add(pBookId);
                                            
                                            var pSubjectId = command.CreateParameter();
                                            pSubjectId.ParameterName = "@subjectId";
                                            pSubjectId.Value = subjectId;
                                            command.Parameters.Add(pSubjectId);
                                            
                                            command.ExecuteNonQuery();
                                        }
                                    }
                                }
                                
                                transaction.Commit();
                            }
                            catch (Exception ex)
                            {
                                transaction.Rollback();
                                throw new Exception("Error updating book: " + ex.Message, ex);
                            }
                        }
                    }
                    
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Explore Edit (POST) action");
                    ViewBag.DatabaseError = "Could not save book changes. Error: " + ex.Message;
                    
                    // Re-populate the dropdown lists
                    try
                    {
                        var authors = new List<Author>();
                        var subjects = new List<Subject>();
                        
                        using (var connection = _context.Database.GetDbConnection())
                        {
                            connection.Open();
                            
                            // Get all authors
                            using (var command = connection.CreateCommand())
                            {
                                command.CommandText = "SELECT author_id, name FROM author ORDER BY name";
                                
                                using (var reader = command.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        authors.Add(new Author
                                        {
                                            AuthorId = reader.GetInt32(0),
                                            Name = reader.GetString(1)
                                        });
                                    }
                                }
                            }
                            
                            // Get all subjects
                            using (var command = connection.CreateCommand())
                            {
                                command.CommandText = "SELECT subject_id, name FROM subject ORDER BY name";
                                
                                using (var reader = command.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        subjects.Add(new Subject
                                        {
                                            SubjectId = reader.GetInt32(0),
                                            Name = reader.GetString(1)
                                        });
                                    }
                                }
                            }
                        }
                        
                        ViewBag.Authors = new SelectList(authors, "AuthorId", "Name");
                        ViewBag.Subjects = new SelectList(subjects, "SubjectId", "Name");
                        ViewBag.SelectedAuthors = selectedAuthors;
                        ViewBag.SelectedSubjects = selectedSubjects;
                    }
                    catch
                    {
                        // If we can't get the dropdown data, just show the error
                    }
                }
            }
            
            return View(book);
        }

        // GET: Book/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                Book book = null;
                
                await ExecuteWithRetryAsync(async () => {
                    using (var connection = _context.Database.GetDbConnection())
                    {
                        await connection.OpenAsync();
                        
                        // Get book details
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = @"
                                SELECT b.book_id, b.title, b.isbn_13, b.publish_date, b.number_of_pages, b.cover_url, b.description
                                FROM book b
                                WHERE b.book_id = @bookId";
                            
                            var parameter = command.CreateParameter();
                            parameter.ParameterName = "@bookId";
                            parameter.Value = id;
                            command.Parameters.Add(parameter);
                            
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    book = new Book
                                    {
                                        BookId = reader.GetInt32(0),
                                        Title = reader.GetString(1),
                                        ISBN13 = !reader.IsDBNull(2) ? reader.GetString(2) : null,
                                        PublishDate = !reader.IsDBNull(3) ? reader.GetDateTime(3) : null,
                                        NumberOfPages = !reader.IsDBNull(4) ? reader.GetInt32(4) : null,
                                        CoverUrl = !reader.IsDBNull(5) ? reader.GetString(5) : null,
                                        Description = !reader.IsDBNull(6) ? reader.GetString(6) : null,
                                        BookAuthors = new List<BookAuthor>(),
                                        BookSubjects = new List<BookSubject>()
                                    };
                                }
                            }
                        }
                        
                        if (book == null)
                        {
                            return false;
                        }
                        
                        // Get authors for the book
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = @"
                                SELECT a.author_id, a.name
                                FROM author a
                                JOIN book_author ba ON a.author_id = ba.author_id
                                WHERE ba.book_id = @bookId
                                ORDER BY a.name";
                            
                            var parameter = command.CreateParameter();
                            parameter.ParameterName = "@bookId";
                            parameter.Value = id;
                            command.Parameters.Add(parameter);
                            
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    book.BookAuthors.Add(new BookAuthor
                                    {
                                        BookId = book.BookId,
                                        AuthorId = reader.GetInt32(0),
                                        Author = new Author
                                        {
                                            AuthorId = reader.GetInt32(0),
                                            Name = reader.GetString(1)
                                        }
                                    });
                                }
                            }
                        }
                        
                        // Get subjects for the book
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = @"
                                SELECT s.subject_id, s.name
                                FROM subject s
                                JOIN book_subject bs ON s.subject_id = bs.subject_id
                                WHERE bs.book_id = @bookId
                                ORDER BY s.name";
                            
                            var parameter = command.CreateParameter();
                            parameter.ParameterName = "@bookId";
                            parameter.Value = id;
                            command.Parameters.Add(parameter);
                            
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    book.BookSubjects.Add(new BookSubject
                                    {
                                        BookId = book.BookId,
                                        SubjectId = reader.GetInt32(0),
                                        Subject = new Subject
                                        {
                                            SubjectId = reader.GetInt32(0),
                                            Name = reader.GetString(1)
                                        }
                                    });
                                }
                            }
                        }
                    }
                    return true;
                });
                
                if (book == null)
                {
                    return NotFound();
                }
                
                return View(book);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Explore Delete action");
                ViewBag.DatabaseError = "Could not retrieve book details for deletion. Error: " + ex.Message;
                return View(new Book { BookId = id.Value, Title = "Error Loading Book" });
            }
        }

        // POST: Explore/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                await ExecuteWithRetryAsync(async () => {
                    using (var connection = _context.Database.GetDbConnection())
                    {
                        await connection.OpenAsync();
                        using (var transaction = await connection.BeginTransactionAsync())
                        {
                            try
                            {
                                // Delete author associations first
                                using (var command = connection.CreateCommand())
                                {
                                    command.Transaction = transaction;
                                    command.CommandText = "DELETE FROM book_author WHERE book_id = @bookId";
                                    
                                    var parameter = command.CreateParameter();
                                    parameter.ParameterName = "@bookId";
                                    parameter.Value = id;
                                    command.Parameters.Add(parameter);
                                    
                                    await command.ExecuteNonQueryAsync();
                                }
                                
                                // Delete subject associations
                                using (var command = connection.CreateCommand())
                                {
                                    command.Transaction = transaction;
                                    command.CommandText = "DELETE FROM book_subject WHERE book_id = @bookId";
                                    
                                    var parameter = command.CreateParameter();
                                    parameter.ParameterName = "@bookId";
                                    parameter.Value = id;
                                    command.Parameters.Add(parameter);
                                    
                                    await command.ExecuteNonQueryAsync();
                                }
                                
                                // Delete the book
                                using (var command = connection.CreateCommand())
                                {
                                    command.Transaction = transaction;
                                    command.CommandText = "DELETE FROM book WHERE book_id = @bookId";
                                    
                                    var parameter = command.CreateParameter();
                                    parameter.ParameterName = "@bookId";
                                    parameter.Value = id;
                                    command.Parameters.Add(parameter);
                                    
                                    await command.ExecuteNonQueryAsync();
                                }
                                
                                await transaction.CommitAsync();
                            }
                            catch (Exception ex)
                            {
                                await transaction.RollbackAsync();
                                throw new Exception("Error deleting book: " + ex.Message, ex);
                            }
                        }
                    }
                    return true;
                });
                
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Explore DeleteConfirmed action");
                TempData["ErrorMessage"] = "Could not delete the book. Error: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Book/FetchFromOpenLibrary
        public IActionResult FetchFromOpenLibrary()
        {
            return View();
        }

        // POST: Book/FetchFromOpenLibrary
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FetchFromOpenLibrary(string isbn)
        {
            if (string.IsNullOrEmpty(isbn))
            {
                ModelState.AddModelError("", "ISBN is required");
                return View();
            }

            // Clean ISBN (remove hyphens, spaces)
            isbn = Regex.Replace(isbn, @"[\s-]", "");

            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.GetAsync($"https://openlibrary.org/api/books?bibkeys=ISBN:{isbn}&format=json&jscmd=data");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(content);
                    
                    if (data != null && data.Count > 0)
                    {
                        var bookData = data[$"ISBN:{isbn}"];
                        
                        var book = new Book
                        {
                            Title = bookData.title,
                            // Store both ISBN formats in ISBN13 field since ISBN10 isn't in the database
                            ISBN13 = isbn,
                            Description = bookData.ContainsKey("description") ? (string)bookData.description.value : null,
                            NumberOfPages = bookData.ContainsKey("number_of_pages") ? (int)bookData.number_of_pages : null,
                            CoverUrl = bookData.ContainsKey("cover") ? (string)bookData.cover.large : null,
                            PublishDate = bookData.ContainsKey("publish_date") ? ParsePublishDate((string)bookData.publish_date) : null
                        };

                        // Process authors
                        if (bookData.ContainsKey("authors"))
                        {
                            foreach (var authorData in bookData.authors)
                            {
                                string authorName = authorData.name;
                                var existingAuthor = await _context.Authors.FirstOrDefaultAsync(a => a.Name == authorName);
                                
                                if (existingAuthor == null)
                                {
                                    existingAuthor = new Author { Name = authorName };
                                    _context.Authors.Add(existingAuthor);
                                    await _context.SaveChangesAsync();
                                }
                                
                                // Add to book.BookAuthors for display in the view
                                if (book.BookAuthors == null)
                                {
                                    book.BookAuthors = new List<BookAuthor>();
                                }
                                
                                book.BookAuthors.Add(new BookAuthor
                                {
                                    Author = existingAuthor,
                                    AuthorId = existingAuthor.AuthorId
                                });
                            }
                        }

                        // Process subjects
                        if (bookData.ContainsKey("subjects"))
                        {
                            foreach (var subjectData in bookData.subjects)
                            {
                                string subjectName = subjectData.name;
                                var existingSubject = await _context.Subjects.FirstOrDefaultAsync(s => s.Name == subjectName);
                                
                                if (existingSubject == null)
                                {
                                    existingSubject = new Subject { Name = subjectName };
                                    _context.Subjects.Add(existingSubject);
                                    await _context.SaveChangesAsync();
                                }
                                
                                // Add to book.BookSubjects for display in the view
                                if (book.BookSubjects == null)
                                {
                                    book.BookSubjects = new List<BookSubject>();
                                }
                                
                                book.BookSubjects.Add(new BookSubject
                                {
                                    Subject = existingSubject,
                                    SubjectId = existingSubject.SubjectId
                                });
                            }
                        }

                        ViewData["FetchedBook"] = book;
                        return View("Create", book);
                    }
                    else
                    {
                        ModelState.AddModelError("", $"Book with ISBN {isbn} not found");
                    }
                }
                else
                {
                    ModelState.AddModelError("", $"Error fetching book data: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching book from Open Library API");
                ModelState.AddModelError("", $"Error: {ex.Message}");
            }

            return View();
        }

        private bool BookExists(int id)
        {
            return _context.Books.Any(e => e.BookId == id);
        }
        
        private DateTime? ParsePublishDate(string dateString)
        {
            if (string.IsNullOrEmpty(dateString))
                return null;
                
            // Try to parse various date formats
            if (DateTime.TryParse(dateString, out DateTime result))
                return result;
                
            // Handle just year format
            if (int.TryParse(dateString, out int year))
                return new DateTime(year, 1, 1);
                
            return null;
        }
    }
}
