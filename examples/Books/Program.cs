using Microsoft.EntityFrameworkCore;
using SqlFace.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionStrings = new
{
    BookDbContext = builder.Configuration.GetConnectionString(nameof(BookDbContext)),
};

builder.Services.AddDbContext<BookDbContext>(b => b.UseNpgsql(connectionStrings.BookDbContext));

builder.Services.AddSqlFaceContext<WebApplication>(context => context.WithSchema(builder =>
{
    builder.Source<Book>();
    builder.Source<Review>();
    builder.Source<Author>();
    builder.Resolver<BookResolver>();
})).AddMemoryCache();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<BookDbContext>().Database.EnsureCreated();
}

app.UseHttpsRedirection();

app.MapSqlFace();

app.Run();

public class BookDbContext : DbContext
{
    public BookDbContext(DbContextOptions<BookDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Book>().ToTable("Books");
        builder.Entity<Author>().ToTable("Authors");
        builder.Entity<Review>().ToTable("Reviews");
    }
}

public class BookResolver
{
    private readonly BookDbContext _dbContext;

    public BookResolver(BookDbContext bookDbContext)
    {
        _dbContext = bookDbContext;
    }

    public async Task<IEnumerable<Book>> GetBooksAsync()
    {
        return await _dbContext.Set<Book>().AsNoTracking().ToListAsync();
    }

    public async Task<IEnumerable<Author>> GetAuthorsAsync()
    {
        return await _dbContext.Set<Author>().AsNoTracking().ToListAsync();
    }
    
    public async Task<IEnumerable<Review>> GetReviewsAsync()
    {
        return await _dbContext.Set<Review>().AsNoTracking().ToListAsync();
    }
}

public class Book
{
    public Book()
    {
        Title = null!;
        Description = null!;
    }
    
    public long BookId { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }
}


public class Author
{
    public Author()
    {
        Name = null!;
    }

    public long AuthorId { get; set; }

    public string Name { get; set; }
}

public class Review
{
    public Review()
    {
        Reviewer = null!;
        Commentary = null!;
    }

    public long ReviewId { get; set; }

    public long BookId { get; set; }

    public string Reviewer { get; set; }

    public float Rating { get; set; }

    public string Commentary { get; set; }
}
