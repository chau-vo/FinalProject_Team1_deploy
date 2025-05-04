using System.Diagnostics;
using FinalProject_Team1.Models;
using Microsoft.AspNetCore.Mvc;
using FinalProject_Team1.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace FinalProject_Team1.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly LibraryDbContext _context;

        private static Dictionary<string, object> _cache = new Dictionary<string, object>();
        private static DateTime _cacheExpiration = DateTime.MinValue;
        private const int CACHE_MINUTES = 5;

        public HomeController(ILogger<HomeController> logger, LibraryDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        
        public IActionResult About()
        {
            return View();
        }
        
        public IActionResult DataVisualization()
        {
            try
            {
                // Set default values
                ViewBag.TotalBooks = 0;
                ViewBag.TotalAuthors = 0;
                ViewBag.TotalSubjects = 0;
                ViewBag.AveragePages = 0;
                ViewBag.BooksPerAuthorData = JsonConvert.SerializeObject(new object[0]);
                ViewBag.TopSubjectsData = JsonConvert.SerializeObject(new object[0]);
                ViewBag.PageDistributionData = JsonConvert.SerializeObject(new object[0]);

                // Use raw SQL queries instead of LINQ to ensure compatibility with Supabase
                using (var connection = _context.Database.GetDbConnection())
                {
                    connection.Open();
                    
                    // 1. Get total counts
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM book";
                        ViewBag.TotalBooks = Convert.ToInt32(command.ExecuteScalar());
                    }
                    
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM author";
                        ViewBag.TotalAuthors = Convert.ToInt32(command.ExecuteScalar());
                    }
                    
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM subject";
                        ViewBag.TotalSubjects = Convert.ToInt32(command.ExecuteScalar());
                    }
                    
                    // 2. Get average pages
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT AVG(number_of_pages) FROM book WHERE number_of_pages IS NOT NULL";
                        var result = command.ExecuteScalar();
                        ViewBag.AveragePages = result != DBNull.Value ? Convert.ToDouble(result) : 0;
                    }
                    
                    // 3. Get books per author
                    var booksPerAuthor = new List<object>();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                            SELECT a.name as author_name, COUNT(ba.book_id) as book_count
                            FROM author a
                            JOIN book_author ba ON a.author_id = ba.author_id
                            GROUP BY a.name
                            ORDER BY book_count DESC
                            LIMIT 5";
                        
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                booksPerAuthor.Add(new
                                {
                                    authorName = reader.GetString(0),
                                    bookCount = reader.GetInt32(1)
                                });
                            }
                        }
                    }
                    ViewBag.BooksPerAuthorData = JsonConvert.SerializeObject(booksPerAuthor);
                    
                    // 4. Get top subjects
                    var topSubjects = new List<object>();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                            SELECT s.name as subject_name, COUNT(bs.book_id) as book_count
                            FROM subject s
                            JOIN book_subject bs ON s.subject_id = bs.subject_id
                            GROUP BY s.name
                            ORDER BY book_count DESC
                            LIMIT 5";
                        
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                topSubjects.Add(new
                                {
                                    subjectName = reader.GetString(0),
                                    bookCount = reader.GetInt32(1)
                                });
                            }
                        }
                    }
                    ViewBag.TopSubjectsData = JsonConvert.SerializeObject(topSubjects);
                    
                    // 5. Get page distribution
                    var pageDistribution = new List<object>();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                            SELECT 
                                CASE 
                                    WHEN number_of_pages <= 200 THEN '0-200'
                                    WHEN number_of_pages <= 400 THEN '201-400'
                                    ELSE '400+'
                                END as range,
                                COUNT(*) as count
                            FROM book
                            WHERE number_of_pages IS NOT NULL
                            GROUP BY range
                            ORDER BY range";
                        
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                pageDistribution.Add(new
                                {
                                    range = reader.GetString(0),
                                    count = reader.GetInt32(1)
                                });
                            }
                        }
                    }
                    ViewBag.PageDistributionData = JsonConvert.SerializeObject(pageDistribution);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DataVisualization action");
                
                // Fall back to sample data if there's an error
                UseSampleData();
                ViewBag.UsingSampleData = true;
                ViewBag.DatabaseError = "Error connecting to database: " + ex.Message;
            }
            
            return View();
        }
        
        private void UseSampleData()
        {
            // Sample data for books per author
            var booksPerAuthor = new[]
            {
                new { AuthorName = "Sample Author 1", BookCount = 5 },
                new { AuthorName = "Sample Author 2", BookCount = 3 },
                new { AuthorName = "Sample Author 3", BookCount = 2 }
            };
            
            // Sample data for top subjects
            var topSubjects = new[]
            {
                new { SubjectName = "Fiction", BookCount = 4 },
                new { SubjectName = "Science", BookCount = 3 },
                new { SubjectName = "History", BookCount = 2 }
            };
            
            // Sample data for page distribution
            var pageDistribution = new[]
            {
                new { Range = "0-200", Count = 3 },
                new { Range = "201-400", Count = 2 },
                new { Range = "400+", Count = 1 }
            };
            
            // Set ViewBag properties with sample data
            ViewBag.BooksPerAuthorData = JsonConvert.SerializeObject(booksPerAuthor);
            ViewBag.TopSubjectsData = JsonConvert.SerializeObject(topSubjects);
            ViewBag.PageDistributionData = JsonConvert.SerializeObject(pageDistribution);
            
            // Sample summary statistics
            ViewBag.TotalBooks = 6;
            ViewBag.TotalAuthors = 3;
            ViewBag.TotalSubjects = 3;
            ViewBag.AveragePages = 250;
        }
        
        public IActionResult Search()
        {
            return View();
        }
    }
}
