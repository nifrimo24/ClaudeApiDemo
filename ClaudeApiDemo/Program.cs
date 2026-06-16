using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using Tool = Anthropic.SDK.Common.Tool;

// ──────────────────────────────────────────────────────────────────────────────
// RETO 3-A: Claude en un proyecto real — POST /api/soporte
// ──────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var client = new AnthropicClient(); // lee ANTHROPIC_API_KEY

var systemSimed = new List<SystemMessage>
{
    new SystemMessage("""
        Eres un asistente de soporte para SIMED, un sistema de gestión de pedidos.

        Reglas de negocio:
        - Pedidos 'Pendiente': cancelables dentro de las primeras 24 horas sin necesidad de justificación.
        - Pedidos 'En proceso': la cancelación requiere autorización del supervisor.
        - Pedidos 'Enviados': no se pueden cancelar bajo ninguna circunstancia.
        - Pedidos urgentes: tienen un cargo adicional del 15 % sobre el total.

        Crédito:
        - Límite inicial para clientes nuevos: $5,000 USD, revisable a los 90 días.
        - Pedidos que superan el límite quedan bloqueados hasta preaprobación del área de crédito.

        Devoluciones:
        - Plazo: 30 días desde la fecha de entrega confirmada.
        - Productos dañados: el cliente debe reportarlos dentro de las 48 horas con foto del daño.
        - Reembolsos aprobados se procesan en 5 a 7 días hábiles.

        Responde siempre en español, de forma clara y directa.
        Si la pregunta está fuera del contexto de SIMED, indica amablemente que no tienes esa información.
        """)
};

app.MapPost("/api/soporte", async (SoporteRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Pregunta))
        return Results.BadRequest(new { error = "La pregunta no puede estar vacía." });

    var response = await client.Messages.GetClaudeMessageAsync(new MessageParameters
    {
        Model = AnthropicModels.Claude46Sonnet,
        MaxTokens = 1024,
        System = systemSimed,
        Messages = new List<Message> { new Message(RoleType.User, req.Pregunta) }
    });

    return Results.Ok(new SoporteResponse(
        response.Message.ToString(),
        new TokenUsage(response.Usage.InputTokens, response.Usage.OutputTokens)
    ));
});

app.Run();

// ──────────────────────────────────────────────────────────────────────────────
// DEMOS - Sesión 3 (referencia)
// ──────────────────────────────────────────────────────────────────────────────

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

    try
    {
        var doc = JsonDocument.Parse(json);
        var cliente = doc.RootElement.GetProperty("cliente").GetString();
        var monto = doc.RootElement.GetProperty("monto").GetDecimal();
        var requiere = doc.RootElement.GetProperty("requiereAprobacion").GetBoolean();

        Console.WriteLine($"\nCliente extraído: {cliente}");
        Console.WriteLine($"Monto: {monto:C}");
        Console.WriteLine($"Requiere aprobación: {requiere}");
    }
    catch (JsonException ex)
    {
        Console.WriteLine($"Error al parsear JSON: {ex.Message}");
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// DEMOS - Sesión 4 (referencia)
// ──────────────────────────────────────────────────────────────────────────────

async Task Demo1_ToolFechaHora()
{
    Console.WriteLine("=== DEMO 1: Tool — fecha y hora ===\n");

    var toolFecha = new Function(
        "get_current_datetime",
        "Returns the current server date and time. Call this whenever the user needs to know the current time or date.",
        JsonNode.Parse("""{"type":"object","properties":{},"required":[]}""")
    );

    var messages = new List<Message>
    {
        new Message(RoleType.User, "Qué hora exacta es ahora mismo?")
    };

    var response = await client.Messages.GetClaudeMessageAsync(new MessageParameters
    {
        Model = AnthropicModels.Claude46Sonnet,
        MaxTokens = 500,
        Messages = messages,
        Tools = new List<Tool>() { toolFecha }
    });

    Console.WriteLine($"StopReason: {response.StopReason}");
    Console.WriteLine($"Bloques en la respuesta: {response.Content.Count}");

    foreach (var block in response.Content)
        Console.WriteLine($"  → {block.GetType().Name}");

    if (response.StopReason == "tool_use")
    {
        var toolCall = response.Content.OfType<ToolUseContent>().First();
        Console.WriteLine($"\nClaude solicitó: {toolCall.Name}");
        Console.WriteLine($"Tool ID: {toolCall.Id}");

        var resultado = DateTime.Now.ToString("HH:mm:ss 'del' dd/MM/yyyy");
        Console.WriteLine($"Resultado de la función: {resultado}");

        messages.Add(response.Message);
        messages.Add(new Message
        {
            Role = RoleType.User,
            Content = new List<ContentBase>
            {
                new ToolResultContent
                {
                    ToolUseId = toolCall.Id,
                    Content = new List<ContentBase>
                    {
                        new TextContent { Text = resultado }
                    }
                }
            }
        });

        var response2 = await client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = AnthropicModels.Claude46Sonnet,
            MaxTokens = 512,
            Messages = messages,
            Tools = new List<Anthropic.SDK.Common.Tool> { toolFecha }
        });

        Console.WriteLine($"\nRespuesta final de Claude:\n{response2.Message}");
        Console.WriteLine($"StopReason final: {response2.StopReason}");
    }
}

async Task Demo2_ToolEstadoPedido()
{
    Console.WriteLine("=== DEMO 2: Tool — estado de pedido SIMED ===\n");

    string ConsultarEstadoPedido(string orderId) =>
        orderId switch
        {
            "ORD-001" => "Pendiente — creado hace 2 horas. Cancelable dentro de las primeras 24h.",
            "ORD-002" => "En proceso — asignado a picking. Requiere autorización del supervisor para cancelar.",
            "ORD-003" => "Enviado — en camino con courier. No se puede cancelar.",
            "ORD-004" => "Entregado — entregado el 09/06/2026. Proceso cerrado.",
            _ => "Pedido no encontrado en el sistema."
        };

    var toolPedido = new Function(
        "get_order_status",
        "Returns the current status of a SIMED order and the applicable cancellation policy. " +
        "Call this when the user asks about order status, cancellation options, or delivery stage. " +
        "Returns status and cancellation rules for that status.",
        JsonNode.Parse("""
                       {
                           "type": "object",
                           "properties": {
                               "order_id": {
                                   "type": "string",
                                   "description": "The SIMED order ID. Format: ORD-NNN (e.g., ORD-001)"
                               }
                           },
                           "required": ["order_id"]
                       }
                       """)
    );

    var systemMessages = new List<SystemMessage>
    {
        new SystemMessage("Eres un asistente de soporte de SIMED. " +
                          "Usa los tools disponibles para consultar datos reales antes de responder. " +
                          "Responde en español de forma clara y útil para el cliente.")
    };

    var preguntas = new[]
    {
        "¿Puedo cancelar el pedido ORD-002?",
        "¿Qué pasó con mi pedido ORD-004?"
    };

    foreach (var pregunta in preguntas)
    {
        Console.WriteLine($"─── Usuario: {pregunta}");

        var messages = new List<Message>
        {
            new Message(RoleType.User, pregunta)
        };

        var response1 = await client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = AnthropicModels.Claude46Sonnet,
            MaxTokens = 512,
            System = systemMessages,
            Messages = messages,
            Tools = new List<Tool> { toolPedido }
        });

        if (response1.StopReason == "tool_use")
        {
            var toolCall = response1.Content.OfType<ToolUseContent>().First();

            var orderId = toolCall.Input["order_id"]?.ToString() ?? "";
            Console.WriteLine($"    [Tool call → {toolCall.Name}('{orderId}')]");

            var estadoReal = ConsultarEstadoPedido(orderId);
            Console.WriteLine($"    [BD retorna → '{estadoReal}']");

            messages.Add(response1.Message);
            messages.Add(new Message
            {
                Role = RoleType.User,
                Content = new List<ContentBase>
                {
                    new ToolResultContent
                    {
                        ToolUseId = toolCall.Id,
                        Content = new List<ContentBase>
                        {
                            new TextContent { Text = estadoReal }
                        }
                    }
                }
            });

            var response2 = await client.Messages.GetClaudeMessageAsync(new MessageParameters
            {
                Model = AnthropicModels.Claude46Sonnet,
                MaxTokens = 512,
                System = systemMessages,
                Messages = messages,
                Tools = new List<Tool> { toolPedido }
            });

            Console.WriteLine($"    Asistente: {response2.Message}\n");
        }
        else
        {
            Console.WriteLine($"    Asistente (sin tool): {response1.Message}\n");
        }
    }
}

async Task Demo3_Chaining()
{
    Console.WriteLine("=== DEMO 3: Chaining workflow ===\n");

    var prompt1 = """
                  Escribe un mensaje de respuesta al cliente para este reclamo:
                  "Mi pedido ORD-001 debía llegar ayer y no ha llegado. Necesito saber qué pasó."

                  Sé amable y proporciona una respuesta de 2-3 oraciones.
                  """;

    var response1 = await client.Messages.GetClaudeMessageAsync(new MessageParameters
    {
        Model = AnthropicModels.Claude46Sonnet,
        MaxTokens = 512,
        Messages = new List<Message> { new Message(RoleType.User, prompt1) }
    });

    var borrador = response1.Message.ToString();
    Console.WriteLine($"PASO 1 — BORRADOR:\n{borrador}\n");
    Console.WriteLine("─".PadRight(60, '─'));

    var prompt2 = $"""
                   Eres un editor de calidad para mensajes de soporte al cliente de SIMED.
                   Revisa el siguiente mensaje y aplica estas correcciones si aplican:

                   1. Si el mensaje promete una fecha de entrega específica: elimínala
                      (no podemos garantizarla sin verificar con logística).
                   2. Si usa la palabra "disculpa" o "lo siento" más de una vez: dejar solo uno.
                   3. Si no incluye un número de referencia: agregar al final
                      "(Referencia caso: {DateTime.Now:yyyyMMddHHmm})"
                   4. Si los próximos pasos no son concretos: hacer más específicos.

                   Si el mensaje ya cumple todos los criterios, devuélvelo sin cambios.

                   Mensaje a revisar:
                   ---
                   {borrador}
                   ---

                   Devuelve solo el mensaje corregido, sin explicaciones.
                   """;

    var response2 = await client.Messages.GetClaudeMessageAsync(new MessageParameters
    {
        Model = AnthropicModels.Claude46Sonnet,
        MaxTokens = 512,
        Messages = new List<Message> { new Message(RoleType.User, prompt2) }
    });

    Console.WriteLine($"\nPASO 2 — REVISADO:\n{response2.Message}");
    Console.WriteLine(
        $"\nTokens de salida — Paso 1: {response2.Usage.OutputTokens} · Paso 2: {response2.Usage.OutputTokens}");
}

async Task Demo4_Routing()
{
    Console.WriteLine("=== DEMO 4: Routing workflow ===\n");

    var promptsEspecializados = new Dictionary<string, string>
    {
        ["tracking"] =
            "Eres un especialista en logística de SIMED. " +
            "Ayuda al cliente con su consulta sobre envío o entrega. " +
            "Si necesitas el número de seguimiento del courier, indícale cómo obtenerlo.",

        ["reclamo"] =
            "Eres el equipo de calidad de SIMED. " +
            "Toma el reclamo con seriedad y empatía. " +
            "Solicita: foto del producto dañado y número de pedido. " +
            "Informa que el proceso de reposición tarda 3-5 días hábiles.",

        ["cancelacion"] =
            "Eres el equipo de cancelaciones de SIMED. " +
            "Aplica estas reglas: Pendiente = cancelable en las primeras 24h; " +
            "En proceso = requiere autorización del supervisor; " +
            "Enviado = no se puede cancelar. " +
            "Pide el número de pedido si no lo tienes.",

        ["informacion"] =
            "Eres el asistente de información general de SIMED. " +
            "Responde de forma concisa. " +
            "Horario de atención: lunes a viernes 8h-18h. " +
            "Para temas específicos de pedidos, transfiere al área correspondiente."
    };

    var consultas = new[]
    {
        "¿Cuándo llega mi pedido ORD-003?",
        "El producto que recibí llegó completamente roto.",
        "Quiero cancelar el pedido ORD-001 que hice hoy.",
        "¿Atienden los sábados?"
    };

    var promptClasificador = """
                             Clasifica la siguiente consulta de cliente en exactamente una de estas categorías:
                             - tracking (preguntas sobre estado de pedido, envío, tracking, entrega)
                             - reclamo (productos dañados, incorrectos, problemas de calidad)
                             - cancelacion (solicitudes para cancelar un pedido)
                             - informacion (preguntas generales: horarios, políticas, cómo funciona el servicio)

                             Responde SOLO con la categoría en minúsculas, sin explicación, sin puntuación.

                             Consulta: {0}
                             """;

    foreach (var consulta in consultas)
    {
        Console.WriteLine($"─── Consulta: \"{consulta}\"");

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
        Console.WriteLine($"    Categoría detectada: [{categoria}]");

        if (promptsEspecializados.TryGetValue(categoria, out var systemPrompt))
        {
            var respFinal = await client.Messages.GetClaudeMessageAsync(new MessageParameters
            {
                Model = AnthropicModels.Claude46Sonnet,
                MaxTokens = 200,
                System = new List<SystemMessage> { new SystemMessage(systemPrompt) },
                Messages = new List<Message> { new Message(RoleType.User, consulta) }
            });
            Console.WriteLine($"    Respuesta: {respFinal.Message}\n");
        }
        else
        {
            Console.WriteLine($"    [Categoría no reconocida: '{categoria}']\n");
        }
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// DEMOS - Sesión 5 (referencia)
// ──────────────────────────────────────────────────────────────────────────────

async Task Demo1_PromptEval()
{
    Console.WriteLine("=== DEMO 1: Evaluación de prompts ===\n");

    var promptTemplateB = "Responde con un ejemplo concreto: {0}";

    var dataset = new[]
    {
        "¿Cuánto es el 15% de 240?",
        "¿Cómo invierto una cadena de texto en C#?",
        "¿Cuáles son las principales causas de la inflación?",
        "¿Cómo hago una petición HTTP básica en .NET?"
    };

    var puntuaciones = new List<double>();

    foreach (var input in dataset)
    {
        var promptCompleto = string.Format(promptTemplateB, input);

        var respuesta = await client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = AnthropicModels.Claude46Sonnet,
            MaxTokens = 512,
            Messages = new List<Message> { new Message(RoleType.User, promptCompleto) }
        });

        var output = respuesta.Message.ToString();
        Console.WriteLine($"Respuesta ({output.Length} chars): {output[..Math.Min(80, output.Length)]}...");

        var promptGrader = $"""
                            Eres un evaluador experto de respuestas de inteligencia artificial.
                            Evalúa la siguiente respuesta generada por un modelo de IA.

                            Pregunta: {input}

                            Respuesta del modelo: {output}

                            Proporciona tu evaluación en este formato exacto:
                            Fortalezas: [1-2 puntos fuertes de la respuesta]
                            Debilidades: [1-2 áreas de mejora]
                            Razonamiento: [1 oración explicando tu puntuación]
                            Puntuación: [número entre 1 y 10]
                            """;

        var gradingInputs = await client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = AnthropicModels.Claude45Haiku,
            MaxTokens = 200,
            Messages = new List<Message> { new Message(RoleType.User, promptGrader) }
        });

        var gradingText = gradingInputs.Message.ToString();
        var puntuacion = ExtraerPuntuacion(gradingText);
        puntuaciones.Add(puntuacion);

        Console.WriteLine($"Puntuación: {puntuacion}/10");
        Console.WriteLine();
    }

    var promedio = puntuaciones.Average();
    Console.WriteLine($"═══ Puntuación promedio del prompt: {promedio:F2}/10 ═══");
    Console.WriteLine("\nModifica el promptTemplate y vuelve a correr — ¿sube la puntuación?");
}

static double ExtraerPuntuacion(string texto)
{
    foreach (var linea in texto.Split('\n'))
    {
        var l = linea.Trim();
        if (l.StartsWith("Puntuación:", StringComparison.OrdinalIgnoreCase))
        {
            var partes = l.Split(':');
            if (partes.Length >= 2 && double.TryParse(partes[1].Trim(), out var n))
                return n;
        }
    }
    return 5.0;
}

async Task Demo2_ExtendedThinking()
{
    Console.WriteLine("=== DEMO 2: Extended thinking ===\n");

    var problema = """
                   Una empresa tiene 3 bodegas. La bodega A tiene 150 unidades del producto X.
                   La bodega B tiene 80 unidades. La bodega C tiene 210 unidades.

                   Llegaron dos pedidos:
                   - Pedido 1: 200 unidades, debe salir de una sola bodega si es posible
                   - Pedido 2: 100 unidades, puede salir de varias bodegas

                   Costo de envío por unidad: A=$2, B=$3, C=$1.5

                   ¿Cuál es la estrategia óptima para minimizar el costo total de envío?
                   Muestra tu razonamiento paso a paso.
                   """;

    Console.WriteLine("Problema:\n" + problema);
    Console.WriteLine("\n[Con extended thinking habilitado...]\n");

    var sw = Stopwatch.StartNew();

    var response = await client.Messages.GetClaudeMessageAsync(new MessageParameters
    {
        Model = AnthropicModels.Claude46Sonnet,
        MaxTokens = 8000,
        Messages = new List<Message> { new Message(RoleType.User, problema) },
        Thinking = new ThinkingParameters
        {
            BudgetTokens = 5000
        }
    });

    sw.Stop();

    foreach (var block in response.Content)
    {
        if (block is ThinkingContent thinkingContent)
        {
            var preview = thinkingContent.Thinking.Length > 300
                ? thinkingContent.Thinking[..300] + "..."
                : thinkingContent.Thinking;

            Console.WriteLine($"[BLOQUE DE RAZONAMIENTO — {thinkingContent.Thinking.Length} chars]");
            Console.WriteLine(preview);
            Console.WriteLine();
        }
        else if (block is TextContent textContent)
        {
            Console.WriteLine("=== RESPUESTA FINAL ===");
            Console.WriteLine(textContent.Text);
        }
    }

    Console.WriteLine($"\nTiempo total: {sw.ElapsedMilliseconds}ms");
}

async Task Demo3_ImageSupport()
{
    Console.WriteLine("=== DEMO 3: Soporte de imágenes ===\n");

    var imageURL = "https://imagenes.elpais.com/resizer/v2/5CT77NK57GLI5Z6AGDWQ5HMH3Y.jpg?auth=743039883068bfcaf71a844546b7cec43aaa92da697a4f8b57483c6a4a0b00df&width=1200";

    Console.WriteLine($"Analizando imagen: {imageURL}\n");

    var response = await client.Messages.GetClaudeMessageAsync(new MessageParameters
    {
        Model = AnthropicModels.Claude46Sonnet,
        MaxTokens = 500,
        Messages = new List<Message>
        {
            new Message
            {
                Role = RoleType.User,
                Content = new List<ContentBase>
                {
                    new TextContent
                    {
                        Text = "Describe brevemente esta imagen. ¿Qué tipo de gráfico o visualización es?"
                    },
                    new ImageContent
                    {
                        Source = new ImageSource
                        {
                            Type = SourceType.url,
                            Url = imageURL
                        }
                    }
                }
            }
        }
    });

    Console.WriteLine($"Descripción de Claude:\n{response.Message}");
}

async Task Demo4_PromptCaching()
{
    Console.WriteLine("=== DEMO 4: Prompt caching ===\n");

    var manualTecnico = """
                         MANUAL DE SOPORTE TÉCNICO — VERSIÓN 2.1
                         Departamento de Atención al Cliente y Operaciones
                         Última actualización: enero 2025

                         ══════════════════════════════════════════════════

                         Capítulo 1: Gestión de pedidos

                         1.1 Estados de un pedido
                         Los pedidos pueden estar en los siguientes estados: Pendiente, En proceso, Enviado, Entregado, Cancelado y En disputa.
                         Cada estado tiene implicaciones específicas para las acciones que puede tomar el cliente y el agente de soporte.

                         1.2 Cancelaciones
                         Un pedido en estado Pendiente puede ser cancelado por el cliente sin necesidad de justificación, dentro de las primeras 24 horas desde su creación.
                         Un pedido en estado En proceso requiere la aprobación de un supervisor para poder cancelarse. El agente de soporte debe escalar la solicitud al supervisor de turno mediante el sistema de tickets interno.
                         Un pedido en estado Enviado no puede cancelarse bajo ninguna circunstancia; el cliente deberá esperar la entrega e iniciar una devolución formal.
                         Un pedido en estado Entregado no admite cancelación; solo aplica la política de devoluciones del Capítulo 3.
                         """;

    var preguntas = new[]
    {
        "¿Puede un cliente cancelar un pedido que está actualmente en proceso?",
        "¿Qué ocurre si un pedido supera el límite de crédito del cliente?",
        "¿Cuánto tiempo tiene un cliente para reportar un producto dañado?"
    };

    Console.WriteLine("Haciendo 3 preguntas sobre el mismo documento...\n");

    for (int i = 0; i < preguntas.Length; i++)
    {
        var sw = Stopwatch.StartNew();

        var response = await client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = AnthropicModels.Claude46Sonnet,
            MaxTokens = 500,
            PromptCaching = PromptCacheType.FineGrained,
            System = new List<SystemMessage>
            {
                new SystemMessage("Eres un asistente de soporte técnico. Responde las preguntas basándote en el manual proporcionado."),
                new SystemMessage(manualTecnico, new CacheControl { Type = CacheControlType.ephemeral })
            },
            Messages = new List<Message>
            {
                new Message(RoleType.User, preguntas[i])
            }
        });

        sw.Stop();

        Console.WriteLine($"Pregunta {i + 1}: {preguntas[i]}");
        Console.WriteLine($"Respuesta: {response.Message.ToString()[..Math.Min(100, response.Message.ToString().Length)]}...");
        Console.WriteLine($"Tiempo: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Tokens leídos del caché: {response.Usage.CacheReadInputTokens}");
        Console.WriteLine($"Tokens escritos en caché: {response.Usage.CacheCreationInputTokens}");
        Console.WriteLine();
    }

    Console.WriteLine("Observar: las llamadas 2 y 3 deberían tener tokens leídos del caché > 0");
    Console.WriteLine("y ser más rápidas que la primera.");
}

// ──────────────────────────────────────────────────────────────────────────────
// Records del endpoint (deben ir al final, después de todas las funciones locales)
// ──────────────────────────────────────────────────────────────────────────────

record SoporteRequest(string Pregunta);
record SoporteResponse(string Respuesta, TokenUsage Tokens);
record TokenUsage(int Entrada, int Salida);
