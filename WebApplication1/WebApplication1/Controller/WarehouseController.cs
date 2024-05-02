using System.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Model;

namespace WebApplication1.Controller;

public class WarehouseController
{
    [Route("api/warehouses")]
    [ApiController]
    public class WarehousesController : ControllerBase
    {
        string conString = "Data Source=db-mssql;Initial Catalog=2019SBD;Integrated Security=True;Trust Server Certificate=True";

        [HttpPost]
        public async Task<IActionResult> Post(Post element)
        {
            var productsFromWarehouse = new List<Product_Warehouse>();
            var orders = new List<Order>();
            var products = new List<Product>();
            using (SqlConnection connection = new SqlConnection(conString))
            {
                await connection.OpenAsync();

                SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync();

                try
                {
                    using (var com = new SqlCommand("SELECT * FROM \"Order\";", connection, transaction))
                    {
                        await com.ExecuteNonQueryAsync();

                        var dr = await com.ExecuteReaderAsync();
                        while (await dr.ReadAsync())
                        {
                            orders.Add(new Order
                            {
                                IdOrder = (int)dr["IdOrder"],
                                IdProduct = (int)dr["IdProduct"],
                                Amount = (int)dr["Amount"],
                                CreatedAt = (DateTime)dr["CreatedAt"]
                            });
                        }
                        await dr.CloseAsync();
                    }

                    using (var com2 = new SqlCommand("SELECT * FROM Product_Warehouse;", connection, transaction))
                    {
                        await com2.ExecuteNonQueryAsync();

                        SqlDataReader dr2 = await com2.ExecuteReaderAsync();
                        while (await dr2.ReadAsync())
                        {
                            productsFromWarehouse.Add(new Product_Warehouse
                            {
                                IdOrder = (int)dr2["IdOrder"],
                                IdProduct = (int)dr2["IdProduct"],
                                Amount = (int)dr2["Amount"],
                                CreatedAt = (DateTime)dr2["CreatedAt"],
                                Price = (Decimal)dr2["Price"],
                                IdProductwarehouse = (int)dr2["IdProductwarehouse"],
                                IdWarehouse = (int)dr2["IdWarehouse"]
                            });
                        }
                        await dr2.CloseAsync();
                    }

                    using (var com3 = new SqlCommand("SELECT * FROM Product;", connection, transaction))
                    {
                        await com3.ExecuteNonQueryAsync();

                        SqlDataReader dr3 = await com3.ExecuteReaderAsync();
                        while (await dr3.ReadAsync())
                        {
                            products.Add(new Product
                            {
                                IdProduct = (int)dr3["IdProduct"],
                                Price = (Decimal)dr3["Price"],
                                Description = (string)dr3["Description"],
                                Name = (string)dr3["Name"]
                            });
                        }
                        await dr3.CloseAsync();
                    }

                    IEnumerable<Product> res1 = from p in products
                                                where p.IdProduct == element.IdProduct
                                                select p;

                    IEnumerable<Order> res2 = from o in orders
                                              join pw in productsFromWarehouse on o.IdOrder equals pw.IdOrder
                                              where o.IdProduct == element.IdProduct
                                                    && o.Amount == element.Amount
                                                    && pw.IdProductwarehouse == null
                                                    && DateTime.Compare(o.CreatedAt, element.CreatedAt) < 0
                                              select o;

                    IEnumerable<Product_Warehouse> res3 = from pw in productsFromWarehouse
                                                          where pw.IdWarehouse == element.IdWarehouse
                                                          select pw;

                    if (!res1.Any())
                    {
                        await new SqlCommand("RAISERROR('Invalid parameter: Provided IdProduct does not exist', 18, 0);", connection, transaction).ExecuteNonQueryAsync();
                    }

                    if (!res2.Any())
                    {
                        await new SqlCommand("RAISERROR('Invalid parameter: There is no order to fullfill', 18, 0);", connection, transaction).ExecuteNonQueryAsync();
                    }

                    if (!res3.Any())
                    {
                        await new SqlCommand("RAISERROR('Invalid parameter: Provided IdWarehouse does not exist', 18, 0);", connection, transaction).ExecuteNonQueryAsync();
                    }

                    var update = new SqlCommand("UPDATE 'Order' SET FulfilledAt = @CreatedAt WHERE IdOrder = @IdOrder; ", connection, transaction);
                    update.Parameters.AddWithValue("@IdOrder", res2.First().IdOrder);
                    await update.ExecuteNonQueryAsync();

                    var insert = new SqlCommand("INSERT INTO Product_Warehouse(IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt) VALUES(@IdWarehouse, @IdProduct, @IdOrder, @Amount, @Amount * @Price, @CreatedAt); ", connection, transaction);
                    insert.Parameters.AddWithValue("@IdProduct", element.IdProduct);
                    insert.Parameters.AddWithValue("@Amount", element.Amount);
                    insert.Parameters.AddWithValue("@IdWarehouse", element.IdWarehouse);
                    insert.Parameters.AddWithValue("@CreatedAt", element.CreatedAt);
                    await insert.ExecuteNonQueryAsync();

                }
                catch (SqlException sqlError)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine(sqlError.Message + " " + sqlError.LineNumber);
                    return NotFound(sqlError.Message);
                }

                var idX = "";
                var getIndex = new SqlCommand("Select IDENT_CURRENT('Product_Warehouse') as Idx;", connection, transaction);
                using (var sqlDataReader = await getIndex.ExecuteReaderAsync())
                {
                    while (await sqlDataReader.ReadAsync())
                    {
                        idX = sqlDataReader["Idx"].ToString();
                    }
                }
                await getIndex.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                await connection.CloseAsync();

                return Ok(idX);
            }
        }
    }
}