using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;

namespace ClaudeApiDemo
{
    public class Reto4
    {
        public async Task EjecutarAsync()
        {
            Console.WriteLine(" RETO 4\n");

            var client = new AnthropicClient();

            var promptsEspecializados = new Dictionary<string, string>
            {
                ["tecnico"] =
                    "Eres un ingeniero de soporte técnico L2 para un SaaS de restaurantes. " +
                    "Tu trabajo es ayudar con integraciones de n8n, automatizaciones con LLMs, " +
                    "y errores en el backend (.NET). Sé analítico, pide logs si es necesario " +
                    "y ofrece soluciones a nivel de arquitectura o configuración.",

                ["facturacion"] =
                    "Eres el agente de facturación del SaaS. " +
                    "Manejas cobros de suscripciones, reembolsos y facturas electrónicas. " +
                    "Sé muy empático, claro con los números y recuerda que las devoluciones " +
                    "tardan de 3 a 5 días hábiles en procesarse.",

                ["ventas"] =
                    "Eres un ejecutivo de cuenta (Sales Representative) del SaaS. " +
                    "Tu objetivo es concretar upgrades de plan, agendar demostraciones (demos) " +
                    "y vender nuevas funcionalidades (como el módulo de reservas). " +
                    "Sé persuasivo, entusiasta y enfocado en el valor para el negocio."
            };

            var consultas = new[]
            {
                "El workflow de n8n no está enviando el mensaje de WhatsApp cuando un pedido cambia a 'Preparando'.",
                
                "Me hicieron un doble cobro en la tarjeta corporativa por la mensualidad del mes de mayo.",
                
                "Queremos habilitar el módulo de agendamiento de citas para nuestras 3 sucursales, ¿tienen algún descuento anual?",
                
                "Al intentar hacer una migración en la base de datos en Railway me da un error de Timeout.",
                
                "Quiero cancelar mi suscripción inmediatamente. El LLM tarda mucho en responder y por su culpa estoy perdiendo clientes, devuélvanme mi dinero."
            };

            var promptClasificador = """
                                     Clasifica la siguiente consulta de un cliente de nuestro SaaS de Restaurantes en EXACTAMENTE UNA de estas 3 categorías:
                                     - tecnico (Problemas de código, integraciones, n8n, LLM, infraestructura)
                                     - facturacion (Problemas de cobros, facturas, tarjetas, suscripciones)
                                     - ventas (Interés en nuevos módulos, upgrades, precios, demos)

                                     Responde SOLO con el nombre de la categoría en minúsculas, sin puntos, ni explicaciones.

                                     Consulta: {0}
                                     """;

            foreach (var consulta in consultas)
            {
                Console.WriteLine("────────────────────────────────────────────────────────────────────────");
                Console.WriteLine($"Usuario: \"{consulta}\"");

                try
                {
                    var respClasificacion = await client.Messages.GetClaudeMessageAsync(new MessageParameters
                    {
                        Model = AnthropicModels.Claude45Haiku,
                        MaxTokens = 15,
                        Messages = new List<Message>
                        {
                            new Message(RoleType.User, string.Format(promptClasificador, consulta))
                        }
                    });

                    var categoria = respClasificacion.Message.ToString().Trim().ToLower();
                    Console.WriteLine($"[Router Haiku] -> Redirigiendo a pipeline: {categoria.ToUpper()}");

                    if (promptsEspecializados.TryGetValue(categoria, out var systemPrompt))
                    {
                        var systemMessages = new List<SystemMessage> { new SystemMessage(systemPrompt) };

                        var respFinal = await client.Messages.GetClaudeMessageAsync(new MessageParameters
                        {
                            Model = AnthropicModels.Claude46Sonnet,
                            MaxTokens = 250,
                            System = systemMessages,
                            Messages = new List<Message> { new Message(RoleType.User, consulta) }
                        });

                        Console.WriteLine($"[Agente {categoria}] Respuesta:\n{respFinal.Message}\n");
                    }
                    else
                    {
                        Console.WriteLine($"[Error de Router] Categoría '{categoria}' no es válida.\n");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error de API: {ex.Message}");
                }
            }

            DocumentarErrorClasificacion();
        }

        private void DocumentarErrorClasificacion()
        {
            Console.WriteLine("📄 DOCUMENTACIÓN DE ERROR DE CLASIFICACIÓN (RETO C)");
            Console.WriteLine("Consulta problemática: ");
            Console.WriteLine("\"Quiero cancelar mi suscripción inmediatamente. El LLM tarda mucho en responder y por su culpa estoy perdiendo clientes, devuélvanme mi dinero.\"");
            Console.WriteLine("\nAnálisis del fallo:");
            Console.WriteLine("- Comportamiento esperado: La consulta tiene múltiples intenciones. La causa raíz es un problema técnico de latencia con el LLM, pero la petición final es una cancelación y reembolso (Facturación). Idealmente, debería ir a retención/facturación O escalar como incidente crítico técnico.");
            Console.WriteLine("- Por qué falla el clasificador simple: Al darle solo 3 opciones estrictas, Haiku se ve forzado a adivinar la prioridad de la oración. A menudo lo enviará a 'tecnico' (porque lee 'LLM tarda mucho') ignorando que el cliente exige su dinero, o lo enviará a 'facturacion' y el agente financiero no sabrá cómo diagnosticar el fallo del LLM.");
            Console.WriteLine("- Solución arquitectónica: Para consultas complejas, el router debe permitir categorías múltiples (ej: 'facturacion, tecnico') para crear un ticket cruzado, o tener una categoría de 'escalamiento_quejas' dedicada.");
        }
    }
}