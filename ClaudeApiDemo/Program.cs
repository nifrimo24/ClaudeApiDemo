using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;

var client = new AnthropicClient(); // lee ANTHROPIC_API_KEY

var demo = 8;

switch (demo)
{
    case 1: await Demo1_PrimeraRequest(); break;
    case 2: await Demo2_CompararModelos(); break;
    case 3: await Demo3_MultiTurn(); break;
    case 4: await Demo4_SystemPrompt(); break;
    case 5: await Demo5_PromptEngineering(); break;
    case 6: await Demo6_XmlTags(); break;
    case 7: await Demo7_FewShot(); break;
    case 8: await Demo8_JsonOutput(); break;
}

// DEMOS - Sesión 3
async Task Demo1_PrimeraRequest()
{
    Console.WriteLine("=== DEMO 1: Primera request ===\n");

    var messages = new List<Message>
    {
        new Message(RoleType.User, "Explica qué es Clean Architecture en 2 oraciones.")
    };

    var response = await client.Messages.GetClaudeMessageAsync(new MessageParameters
    {
        Model = AnthropicModels.Claude46Sonnet,
        MaxTokens = 1024,
        Messages = messages,
        Stream = false
    });

    Console.WriteLine(response.Message.ToString());
    Console.WriteLine($"\nTokens — entrada: {response.Usage.InputTokens}, salida: {response.Usage.OutputTokens}");
    Console.WriteLine($"Stop reason: {response.StopReason}");
}

async Task Demo2_CompararModelos()
{
    Console.WriteLine("=== DEMO 2: Comparar modelos ===");

    var prompt =
        "Hay un NullReferenceException en OrderService cuando el customerId no existe. Cual es la causa más probable? Response en máximo 3 líneas";

    foreach (var model in new[] { AnthropicModels.Claude45Haiku, AnthropicModels.Claude46Sonnet })
    {
        var response = await client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = model,
            MaxTokens = 1024,
            Messages = new List<Message> { new Message(RoleType.User, prompt) }
        });

        Console.WriteLine($"--- {model} ---");
        Console.WriteLine(response.Message.ToString());
        Console.WriteLine(
            $"[Tokens usados: {response.Usage.InputTokens} entrada / {response.Usage.OutputTokens} salida]\n");
    }
}

async Task Demo3_MultiTurn()
{
    Console.WriteLine("=== DEMO 3: Multi turnos ===");

    var messages = new List<Message>
    {
        new Message(RoleType.User, "Mi sistema tiene 3 microservicios: Orders, Customers y Catalog")
    };

    // Turno 1
    var response1 = await client.Messages.GetClaudeMessageAsync(new MessageParameters
    {
        Model = AnthropicModels.Claude46Sonnet,
        MaxTokens = 1024,
        Messages = messages
    });

    Console.WriteLine($"Turno 1 - Claude: {response1.Message}\n");

    // Mantener la conversación: agregar la respuesta de Claude al array
    messages.Add(response1.Message);
    messages.Add(new Message(RoleType.User, "Cuantos servicios mencione?"));

    // Turno 2

    var response2 = await client.Messages.GetClaudeMessageAsync(new MessageParameters
    {
        Model = AnthropicModels.Claude46Sonnet,
        MaxTokens = 1024,
        Messages = messages
    });

    Console.WriteLine($"Turno 2 - Claude: {response2.Message}\n");
    Console.WriteLine($"\nMensajes en el array: {messages.Count} Claude recibe todo en cada request");
}

async Task Demo4_SystemPrompt()
{
    Console.WriteLine("=== DEMO 4: System prompt ===");

    var systemMessages = new List<SystemMessage>
    {
        new SystemMessage("""
                          Eres un asistente de soporte para SIMED, un sistema de gestión de pedidos.
                          Reglas de negocio:
                          - Pedidos 'Pendiente': cancelables dentro de las primeras 24 horas.
                          - Pedidos 'En proceso': cancelación solo con autorización del supervisor.
                          - Pedidos 'Enviados': no se pueden cancelar.
                          Responde en español. Si preguntan algo fuera de este contexto, indica que no tienes esa información.
                          """)
    };

    var questions = new[]
    {
        "Puedo cancelar un pedido que está en proceso?",
        "Cuál es el tipo de cambio del dólar hoy?"
    };

    foreach (var question in questions)
    {
        Console.WriteLine($"Usuario: {question}");

        var response = await client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = AnthropicModels.Claude46Sonnet,
            MaxTokens = 1024,
            System = systemMessages,
            Messages = new List<Message> { new Message(RoleType.User, question) }
        });
        Console.WriteLine(response.Message.ToString());
    }
}

async Task Demo5_PromptEngineering()
{
    Console.WriteLine("=== DEMO 5: PromptEngineering ===");

    var lazyPrompt =
        "Revisa este código:\npublic Order CreateOrder(int customerId) { var c = _repo.Get(customerId); return new Order(c.Id); }";

    var specificPrompt = """
                         Revisa este método C# e identifica:
                         1. Errores de manejo de excepciones
                         2. Validaciones de entrada faltantes
                         Responde con lista numerada. Código:

                         public Order CreateOrder(int customerId) {
                             var c = _repo.Get(customerId);
                             return new Order(c.Id);
                         }
                         """;

    foreach (var (etiqueta, prompt) in new[] { ("Vago", lazyPrompt), ("Específico", specificPrompt) })
    {
        Console.WriteLine($"─── Prompt {etiqueta} ───");

        var reponse = await client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = AnthropicModels.Claude46Sonnet,
            MaxTokens = 1024,
            Messages = new List<Message> { new Message(RoleType.User, prompt) }
        });

        Console.WriteLine(reponse.Message.ToString());
        Console.WriteLine();
    }
}

async Task Demo6_XmlTags()
{
    Console.WriteLine("=== DEMO 6: Xml tags ===");

    var prompt = """
                 Analiza el siguiente pedido y devuelve un resumen ejecutivo.

                 <pedido>
                   <numero>ORD-2024-001</numero>
                   <cliente>Distribuidora ABC</cliente>
                   <estado>Pendiente</estado>
                   <items>
                     <item><producto>Producto X</producto><cantidad>50</cantidad><precio>25.00</precio></item>
                     <item><producto>Producto Y</producto><cantidad>20</cantidad><precio>40.00</precio></item>
                   </items>
                 </pedido>

                 <instrucciones>
                 Calcula el total del pedido.
                 Indica si hay algún riesgo basado en el estado.
                 Formato de respuesta: máximo 3 líneas.
                 </instrucciones>
                 """;

    var response = await client.Messages.GetClaudeMessageAsync(new MessageParameters
    {
        Model = AnthropicModels.Claude46Sonnet,
        MaxTokens = 1024,
        Messages = new List<Message> { new Message(RoleType.User, prompt) }
    });

    Console.WriteLine(response.Message.ToString());
}

async Task Demo7_FewShot()
{
    Console.WriteLine("=== DEMO 7: Few Shots ===");

    var withoutFewShot = "Clasifica este error según severidad: 'NullReferenceException en OrderService.cs línea 42'.";

    var withFewShot = """
                      Clasifica errores de aplicación según su severidad y componente.

                      Formato de respuesta: SEVERIDAD | COMPONENTE | ACCIÓN

                      Ejemplos:
                      Input: 'Connection timeout a base de datos'
                      Output: CRÍTICO | Infraestructura | Alertar al equipo de operaciones

                      Input: 'Usuario no encontrado en directorio'
                      Output: MEDIO | Autenticación | Verificar sincronización de usuarios

                      Input: 'NullReferenceException en OrderService.cs línea 42'
                      Output:
                      """;

    foreach (var (etiqueta, prompt) in new[] { ("SIN EJEMPLOS", withoutFewShot), ("CON FEW-SHOT", withFewShot) })
    {
        Console.WriteLine($"─── {etiqueta} ───");

        var response = await client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = AnthropicModels.Claude46Sonnet,
            MaxTokens = 1024,
            Messages = new List<Message> { new Message(RoleType.User, prompt) }
        });

        Console.WriteLine(response.Message.ToString());
        Console.WriteLine();
    }
}

async Task Demo8_JsonOutput()
{
    Console.WriteLine("=== DEMO 8: Structured JSON output ===\n");

    var systemMessages = new List<SystemMessage>
    {
        new SystemMessage(
            "Eres un extractor de datos. Responde únicamente con JSON válido, sin texto adicional ni bloques de código markdown.")
    };

    var resp = await client.Messages.GetClaudeMessageAsync(new MessageParameters
    {
        Model = AnthropicModels.Claude46Sonnet,
        MaxTokens = 512,
        System = systemMessages,
        Messages = new List<Message>
        {
            new Message(RoleType.User, """
                                       Extrae la información del siguiente texto y devuelve este JSON:
                                       {
                                         "cliente": string,
                                         "monto": number,
                                         "estado": string,
                                         "requiereAprobacion": boolean
                                       }

                                       Texto: "El pedido de Distribuidora ABC por $15,000 está pendiente de revisión por exceder el límite de crédito."
                                       """)
        },
        Stream = false
    });

    var json = resp.Message.ToString();
    Console.WriteLine("Respuesta de Claude:");
    Console.WriteLine(json);

    // Parsear el JSON
    var doc = JsonDocument.Parse(json);
    var cliente = doc.RootElement.GetProperty("cliente").GetString();
    var monto = doc.RootElement.GetProperty("monto").GetDecimal();
    var requiere = doc.RootElement.GetProperty("requiereAprobacion").GetBoolean();

    Console.WriteLine($"\nCliente extraído: {cliente}");
    Console.WriteLine($"Monto: {monto:C}");
    Console.WriteLine($"Requiere aprobación: {requiere}");
}

// DEMOS - Sesión 4
