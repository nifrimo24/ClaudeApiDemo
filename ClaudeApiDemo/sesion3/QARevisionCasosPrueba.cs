// sesion3/QARevisionCasosPrueba.cs
// Demo: Revisión de casos de prueba con system prompt refinado para QA
// Patrón: System prompt especializado con criterios estructurados + evaluación de 6 casos

using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;

var client = new AnthropicClient();

await DemoQA_RevisionCasosPrueba();

async Task DemoQA_RevisionCasosPrueba()
{
    Console.WriteLine("=== DEMO QA: Revisión de Casos de Prueba — System Prompt Refinado ===\n");

    // System prompt refinado:
    // Mejoras sobre Demo4_SystemPrompt original:
    // 1. Criterios explícitos y numerados (no solo reglas de negocio generales)
    // 2. Formato de salida obligatorio → facilita el parseo programático
    // 3. Escala de evaluación de 3 niveles en vez de binario
    // 4. Ejemplo inline que ancla el formato esperado (few-shot en el system prompt)
    var systemPromptQA = """
        Eres un experto en QA especializado en revisión de casos de prueba para SIMED,
        un sistema de gestión de pedidos empresarial.

        Evalúa cada caso de prueba contra estos 5 criterios:
        1. PRECONDICIONES — ¿El estado inicial del sistema está claramente definido?
        2. PASOS — ¿Son específicos, ordenados y ejecutables sin ambigüedad?
        3. RESULTADO ESPERADO — ¿Es observable y verificable por un tester?
        4. COBERTURA — ¿El caso prueba una sola cosa (responsabilidad única)?
        5. DATOS DE PRUEBA — ¿Se especifican IDs, montos, roles y valores concretos?

        Reglas de negocio de SIMED que debes tener en cuenta al evaluar:
        - Pedido Pendiente: cancelable dentro de las primeras 24 h sin autorización.
        - Pedido En proceso: requiere autorización del supervisor para cancelar.
        - Pedido Enviado: no cancelable bajo ninguna circunstancia.
        - Pedidos urgentes: cargo adicional del 15 %; solo supervisores pueden marcarlos.
        - Crédito: pedidos que superan el límite quedan en estado "Bloqueado", no "Pendiente".
        - Producto dañado: debe reportarse dentro de las 48 h con foto obligatoria.

        Responde SIEMPRE en este formato exacto (sin texto adicional fuera del formato):

        ESTADO: [APROBADO | RECHAZADO | NECESITA_MEJORAS]
        CRITERIOS:
        - Precondiciones: [OK | FALTA | INCOMPLETO] — <observación en ≤10 palabras>
        - Pasos: [OK | FALTA | INCOMPLETO] — <observación en ≤10 palabras>
        - Resultado esperado: [OK | FALTA | INCOMPLETO] — <observación en ≤10 palabras>
        - Cobertura: [OK | EXCEDE | INCOMPLETO] — <observación en ≤10 palabras>
        - Datos de prueba: [OK | FALTA | INCOMPLETO] — <observación en ≤10 palabras>
        SUGERENCIA: <una acción concreta para mejorar, o "Ninguna" si está aprobado>

        Ejemplo de respuesta correcta:
        ESTADO: RECHAZADO
        CRITERIOS:
        - Precondiciones: FALTA — No indica estado actual del pedido
        - Pasos: OK — Claros y secuenciales
        - Resultado esperado: FALTA — No describe qué debe mostrar el sistema
        - Cobertura: OK — Prueba un único flujo
        - Datos de prueba: FALTA — No especifica ID de pedido ni usuario
        SUGERENCIA: Agregar estado "Enviado", resultado esperado y el ID ORD-NNN en los datos
        """;

    // 6 casos de prueba con distintos niveles de calidad
    var casosDePrueba = new[]
    {
        // TC-001: Bien escrito — debería APROBAR
        (id: "TC-001", descripcion: """
            Título: Cancelar pedido en estado Pendiente dentro de las 24 horas
            Precondiciones:
              - Usuario autenticado con rol "Cliente"
              - Pedido ORD-001 en estado "Pendiente", creado hace 2 horas
            Pasos:
              1. Navegar a "Mis Pedidos"
              2. Seleccionar el pedido ORD-001
              3. Hacer clic en "Cancelar pedido"
              4. Confirmar la cancelación en el diálogo emergente
            Resultado esperado:
              - El pedido ORD-001 cambia a estado "Cancelado"
              - Se muestra el mensaje "Pedido cancelado exitosamente"
              - El cliente recibe un correo electrónico de confirmación
            Datos de prueba: ORD-001, usuario: cliente01@simed.com
            """),

        // TC-002: Sin resultado esperado — debería RECHAZARSE
        (id: "TC-002", descripcion: """
            Título: Intentar cancelar pedido en estado Enviado
            Precondiciones:
              - Pedido ORD-003 en estado "Enviado"
            Pasos:
              1. Acceder al pedido ORD-003
              2. Intentar cancelar el pedido usando el botón "Cancelar"
            """),

        // TC-003: Pasos ambiguos y sin datos concretos — NECESITA_MEJORAS
        (id: "TC-003", descripcion: """
            Título: Modificar dirección de envío de un pedido activo
            Precondiciones:
              - Hay un pedido activo en el sistema
            Pasos:
              1. Buscar el pedido
              2. Editar los datos de envío
              3. Guardar los cambios
            Resultado esperado:
              - Los datos se actualizan correctamente en el sistema
            """),

        // TC-004: Cubre múltiples flujos en un solo caso — debería indicar EXCEDE cobertura
        (id: "TC-004", descripcion: """
            Título: Proceso completo: pedido urgente con validación de crédito y posterior devolución
            Precondiciones:
              - Usuario autenticado con rol "Supervisor"
              - Cliente "Distribuidora ABC" con límite de crédito de $10,000 y crédito disponible de $9,000
            Pasos:
              1. Crear pedido nuevo para "Distribuidora ABC" por $8,000
              2. Marcar el pedido como urgente
              3. Verificar que se aplica cargo adicional del 15 % ($1,200)
              4. Confirmar que el monto total ($9,200) no supera el crédito disponible ($9,000)
              5. Confirmar el pedido y esperar el envío
              6. Registrar la recepción del pedido
              7. Iniciar el proceso de devolución del pedido completo
              8. Verificar que el reembolso se procesa al método de pago original en 5-7 días hábiles
            Resultado esperado:
              - Todo el flujo se completa sin errores del sistema
            """),

        // TC-005: Bien escrito para validación de crédito — debería APROBAR
        (id: "TC-005", descripcion: """
            Título: Bloqueo automático de pedido que supera el límite de crédito
            Precondiciones:
              - Cliente "Distribuidora ABC" con límite de crédito $5,000 y crédito disponible $1,000
              - Usuario autenticado con rol "Agente de soporte"
            Pasos:
              1. Iniciar creación de nuevo pedido para "Distribuidora ABC"
              2. Ingresar monto de $3,000 (supera los $1,000 disponibles)
              3. Intentar confirmar el pedido
            Resultado esperado:
              - El sistema bloquea el pedido automáticamente
              - El pedido queda en estado "Bloqueado" (no "Pendiente")
              - Se muestra el mensaje "Pedido bloqueado: supera el límite de crédito disponible"
              - El agente ve las opciones: pago parcial anticipado, reducción del pedido, solicitar incremento temporal
            Datos de prueba: cliente ID: CLI-042, monto: $3,000, crédito disponible: $1,000
            """),

        // TC-006: Sin precondiciones y resultado esperado incompleto — NECESITA_MEJORAS
        (id: "TC-006", descripcion: """
            Título: Reporte de producto dañado recibido
            Pasos:
              1. El cliente contacta al soporte y reporta que el producto llegó dañado
              2. El agente solicita foto del producto dañado
              3. El cliente adjunta la fotografía al reporte
              4. El agente envía el reporte al equipo de calidad
            Resultado esperado:
              - Se genera un ticket de devolución en el sistema
            """),
    };

    var resumen = new List<(string id, string estado, string sugerencia)>();

    foreach (var (id, descripcion) in casosDePrueba)
    {
        Console.WriteLine($"─── Evaluando {id} ───");
        var titulo = descripcion.Split('\n').First(l => l.Trim().StartsWith("Título:")).Trim();
        Console.WriteLine(titulo);

        var response = await client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = AnthropicModels.Claude46Sonnet,
            MaxTokens = 350,
            System = new List<SystemMessage> { new SystemMessage(systemPromptQA) },
            Messages = new List<Message>
            {
                new Message(RoleType.User, $"Revisa el siguiente caso de prueba:\n\n{descripcion}")
            }
        });

        var evaluacion = response.Message.ToString();
        Console.WriteLine(evaluacion);
        Console.WriteLine();

        var estado = ExtraerValorLinea(evaluacion, "ESTADO:");
        var sugerencia = ExtraerValorLinea(evaluacion, "SUGERENCIA:");
        resumen.Add((id, estado, sugerencia));
    }

    // Resumen ejecutivo al final
    Console.WriteLine(new string('═', 55));
    Console.WriteLine("RESUMEN EJECUTIVO — REVISIÓN QA");
    Console.WriteLine(new string('═', 55));

    foreach (var (id, estado, sugerencia) in resumen)
    {
        var icono = estado switch
        {
            var s when s.Contains("APROBADO")        => "[OK]",
            var s when s.Contains("RECHAZADO")       => "[X] ",
            var s when s.Contains("NECESITA_MEJORAS") => "[~] ",
            _                                        => "[?] "
        };
        Console.WriteLine($"{icono} {id}: {estado}");
        if (!sugerencia.Contains("Ninguna", StringComparison.OrdinalIgnoreCase) && sugerencia != "No encontrado")
            Console.WriteLine($"      Acción: {sugerencia}");
    }

    var aprobados = resumen.Count(r => r.estado.Contains("APROBADO", StringComparison.OrdinalIgnoreCase));
    var rechazados = resumen.Count(r => r.estado.Contains("RECHAZADO", StringComparison.OrdinalIgnoreCase));
    var mejoras = resumen.Count(r => r.estado.Contains("NECESITA_MEJORAS", StringComparison.OrdinalIgnoreCase));

    Console.WriteLine($"\nTotal: {aprobados} aprobados · {rechazados} rechazados · {mejoras} con mejoras · de {resumen.Count} casos");
}

static string ExtraerValorLinea(string texto, string prefijo)
{
    foreach (var linea in texto.Split('\n'))
    {
        var l = linea.Trim();
        if (l.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
            return l[prefijo.Length..].Trim();
    }
    return "No encontrado";
}
