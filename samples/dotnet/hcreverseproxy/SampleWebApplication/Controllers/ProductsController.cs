using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using System.Net;


namespace SampleWebApplication.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProductsController : ControllerBase
    {
        static List<Product> products = new List<Product>
        {
            new Product { Id = 1, Name = "Tomato Soup", Category = "Groceries", Price = 1 },
            new Product { Id = 2, Name = "Yo-yo", Category = "Toys", Price = 3.75M },
            new Product { Id = 3, Name = "Hammer", Category = "Hardware", Price = 16.99M }
        };

        [HttpGet]
        public IEnumerable<Product> GetAllProducts()
        {
            Console.WriteLine($"GET {this.Request.GetDisplayUrl()}");
            return products;
        }

        [HttpGet("id")]
        public Product GetProductById(int id)
        {
            Console.WriteLine($"GET {this.Request.GetDisplayUrl()}");
            var product = products.FirstOrDefault((p) => p.Id == id);
            if (product == null)
            {
                throw new HttpRequestException(HttpStatusCode.NotFound.ToString());
            }
            return product;
        }

        [HttpGet("category")]
        public IEnumerable<Product> GetProductsByCategory(string category)
        {
            Console.WriteLine($"GET {this.Request.GetDisplayUrl()}");
            return products.Where(p => string.Equals(p.Category, category,
                    StringComparison.OrdinalIgnoreCase));
        }
    }
}
