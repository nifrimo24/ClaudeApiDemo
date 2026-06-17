using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;

namespace ClaudeApiDemo
{
    public class Reto3
    {
        public async Task EjecutarAsync()
        {
            Console.WriteLine("Reto 3\n");

            var client = new AnthropicClient();

            var promptVago = """
                             Revisa esta función para calcular el total de una orden y dime si está bien:
                             public decimal Calcular(List<Item> items) { decimal total = 0; foreach(var i in items) total += i.Precio; return total; }
                             """;

            var promptEspecifico = """
                                   Actúa como un Senior .NET Developer. Revisa este método en C# que calcula el total de una orden en una plataforma SaaS para restaurantes. Identifica problemas de lógica comercial (por ejemplo, el manejo de descuentos o cantidades nulas) y sugiere mejoras de rendimiento usando LINQ. Devuelve el código corregido.

                                   Código:
                                   public decimal CalcularTotalOrden(List<ItemOrden> items, string cuponDescuento) { decimal total = 0; foreach(var item in items) { total += item.Precio * item.Cantidad; } if(cuponDescuento == "DESC10") total = total - (total * 0.10m); return total; }
                                   """;

            var promptXml = """
                            Analiza el siguiente código para el módulo de pedidos desarrollado en .NET 8.

                            <codigo>
                            public decimal CalcularTotalOrden(List<ItemOrden> items, string cuponDescuento) {
                                decimal total = 0;
                                foreach(var item in items) {
                                    total += item.Precio * item.Cantidad;
                                }
                                if(cuponDescuento == "DESC10") total = total - (total * 0.10m);
                                return total;
                            }
                            </codigo>

                            <instrucciones>
                            1. Identifica vulnerabilidades o malas prácticas (ej: hardcodeo de reglas de negocio, manejo de nulos).
                            2. Refactoriza el código aplicando Clean Code, manejo de excepciones temprano (Guard Clauses) y LINQ.
                            3. Explica los cambios en un máximo de 3 bullet points concisos.
                            </instrucciones>
                            """;

            var prompts = new[]
            {
                ("1. VAGO", promptVago),
                ("2. ESPECÍFICO", promptEspecifico),
                ("3. XML TAGS", promptXml)
            };

            foreach (var (etiqueta, prompt) in prompts)
            {
                Console.WriteLine($"│ {etiqueta,-44} │\n");

                try
                {
                    var response = await client.Messages.GetClaudeMessageAsync(new MessageParameters
                    {
                        Model = AnthropicModels.Claude46Sonnet,
                        MaxTokens = 1024,
                        Messages = new List<Message> { new Message(RoleType.User, prompt) }
                    });

                    Console.WriteLine(response.Message.ToString());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al consultar la API: {ex.Message}");
                }

                if (etiqueta != "3. XML TAGS")
                {
                    Console.WriteLine("\nEtiqueta no válida");
                    Console.WriteLine("---------------------------------------Nuevo Prompt---------------------------------------\n");
                }
            }


        }
    }
}