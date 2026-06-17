using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;


namespace ClaudeApiDemo.Sesion5;

public class Reto5
{
    public async Task EjecutarAsync()
    {
        var client = new AnthropicClient();
        Console.WriteLine("=== Reto 5: Eval Pipeline (Dominio IT/Seguridad) ===\n");

        // Version A - Prompt básico (Vago)
        var promptTemplateA = "Clasifica este ticket de soporte técnico: {0}";

        // Version B - Prompt mejorado (Estricto)
        var promptTemplateB = """
        Eres un analista de Nivel 1 en un SOC (Centro de Operaciones de Seguridad).
        Tu tarea es clasificar la intención principal del ticket de soporte reportado por el usuario.
        
        Reglas estrictas:
        1. Responde ÚNICAMENTE con una de estas categorías válidas: [Incidente_Phishing, Acceso_Denegado, Falla_Hardware, Solicitud_Software, Otro].
        2. No incluyas saludos, explicaciones, ni puntuación adicional.
        
        Ticket del usuario: "{0}"
        Categoría:
        """;

        // Dataset de prueba (Reducido a 2 casos para agilidad)
        var dataset = new[]
        {
            "He recibido un correo urgente de Recursos Humanos pidiendo que valide mi contraseña en un enlace extraño, creo que es falso.",
            "Mi monitor principal no da imagen desde que hubo un bajón de luz en la oficina."
        };

        Console.WriteLine("Evaluando Versión A (Básica)...");
        var promedioA = await EjecutarEvaluacion(client, promptTemplateA, dataset, AnthropicModels.Claude46Sonnet);

        Console.WriteLine("\nEvaluando Versión B (Mejorada)...");
        var promedioB = await EjecutarEvaluacion(client, promptTemplateB, dataset, AnthropicModels.Claude46Sonnet);

        // Documentación del incremento
        Console.WriteLine("\n=== RESULTADOS FINALES ===");
        Console.WriteLine($"Score Versión A (Actual):   {promedioA:F2} / 10.00");
        Console.WriteLine($"Score Versión B (Mejorada): {promedioB:F2} / 10.00");

        var incremento = promedioB - promedioA;
        Console.WriteLine($"Incremento de calidad:      +{incremento:F2} puntos");
    }

    async Task<double> EjecutarEvaluacion(AnthropicClient client, string promptTemplate, string[] dataset, string modeloGenerador)
    {
        var puntuaciones = new List<double>();

        foreach (var input in dataset)
        {
            var promptCompleto = string.Format(promptTemplate, input);

            // 1. Generar respuesta con el modelo base (Sonnet)
            var respuesta = await client.Messages.GetClaudeMessageAsync(new MessageParameters
            {
                Model = modeloGenerador,
                MaxTokens = 150,
                Messages = new List<Message> { new Message(RoleType.User, promptCompleto) }
            });

            var output = respuesta.Message.ToString().Trim();

            Console.WriteLine($"\n  [Ticket Usuario]: \"{input}\"");
            Console.WriteLine($"  [IA Generadora]: {output}");

            // Pausa obligatoria de 15s para evitar Rate Limits (Tier 0)
            await Task.Delay(15000);

            // 2. Model Grader con Haiku
            var promptGrader = $"""
            Eres un auditor experto en sistemas de triage automatizado de TI.
            Evalúa la respuesta generada por una IA que debe extraer la categoría de un ticket de soporte.

            Ticket original: "{input}"
            Respuesta generada por la IA: "{output}"

            Criterios de evaluación (1-10):
            - 10: La IA identificó la categoría perfectamente y respondió ÚNICAMENTE con el nombre de la categoría, ideal para parseo en backend.
            - 5-9: La IA identificó el problema, pero incluyó texto extra conversacional que rompería un sistema de enrutamiento automatizado.
            - 1-4: La IA falló en identificar el problema o dio una respuesta completamente inútil.

            Proporciona tu evaluación en este formato exacto:
            Fortalezas: [1 fortaleza]
            Debilidades: [1 debilidad]
            Razonamiento: [1 oración breve]
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

            // --- VISUALIZACIÓN COMPLETA DEL JUEZ ---
            Console.WriteLine("\n  [Análisis de Haiku]:");
            Console.WriteLine($"  {gradingText.Replace("\n", "\n  ")}");

            Console.WriteLine($"\n  [Veredicto Final]: Nota asignada -> {puntuacion}/10");
            Console.WriteLine("  --------------------------------------------------");

            // Segunda pausa obligatoria antes de procesar el siguiente mensaje
            await Task.Delay(15000);
        }

        return puntuaciones.Average();
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
}