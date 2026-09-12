# AGENTS.md — Pam Automation .NET

## Alcance

Este repositorio es una aplicación nueva construida desde cero para Windows. La aplicación anterior está en C:\pam automatizacion y solo es referencia de lectura.

La raíz oficial del repositorio es `C:\pam automatizacion dotnet\PamAutomation` y la solución utiliza el formato moderno `PamAutomation.slnx`.

## Política de lectura de documentación

`AGENTS.md` es la instrucción permanente y el índice de consulta del repositorio; debe aplicarse cada vez que Codex lo cargue. Esa carga automática no obliga a releer los demás Markdown. La primera vez que un thread nuevo de Codex entra a este proyecto sin contexto previo, el agente principal o coordinador debe leer una vez y por completo:

- CODEX_BUILD_SPEC.md
- README.md
- LOGIC.md
- History.md
- structure.md
- Fases.md
- Subagentes.md
- PAM_NAVIGATION.md

En todos los prompts y tasks posteriores del mismo thread, el agente principal no debe repetir automáticamente esa lectura completa porque conserva el contexto adquirido. Consultará bajo demanda únicamente el documento o la sección que necesite para recordar o verificar una decisión, resolver una duda, preparar una fase o comprobar una posible contradicción. Que el propio agente haya actualizado un Markdown no obliga a releerlo por completo; basta revisar el cambio realizado, salvo que exista una razón concreta para volver a consultar el documento.

Una nueva tarea, fase o solicitud dentro del mismo thread no reinicia esta obligación. La lectura completa solo debe realizarse de nuevo cuando otro thread o coordinador entra sin contexto previo o sin un traspaso confiable, el contexto disponible ya no permite confirmar las reglas vigentes o el usuario la solicita expresamente.

Usar este índice para seleccionar la lectura mínima necesaria:

- `CODEX_BUILD_SPEC.md`: requisitos globales, tecnología, seguridad y arquitectura.
- `README.md`: estado general, entorno y comandos de uso actuales.
- `LOGIC.md`: comportamiento funcional, reglas de negocio y contratos.
- `History.md`: origen cronológico de decisiones y contradicciones.
- `structure.md`: estructura física, responsabilidades y dependencias entre proyectos.
- `Fases.md`: alcance completado, fase actual, dependencias pendientes y oportunidades condicionadas de paralelismo.
- `Subagentes.md`: delegación, contexto, aislamiento, integración y límites multiagente.
- `PAM_NAVIGATION.md`: recorrido observado del portal, selectores, invariantes de navegación y base prevista para colecciones.

Esta lectura completa tampoco se repite automáticamente en cada subagente. Los subagentes deben obedecer este `AGENTS.md` y recibir del coordinador un paquete de contexto acotado con la ruta de trabajo, la rama, el objetivo, los archivos permitidos, las restricciones, los criterios de aceptación y los archivos o secciones documentales que deben leer. No deben recorrer todo el repositorio ni leer todos los Markdown por defecto.

Un subagente solo leerá los archivos fuente, pruebas y secciones documentales asignados. Puede ampliar esa lectura únicamente cuando sea imprescindible para resolver una dependencia directa, comprobar seguridad o aclarar una contradicción; debe explicar al coordinador qué contexto adicional necesitó. Si el contexto suministrado no basta para tomar una decisión segura, debe detenerse y solicitarlo en lugar de asumir. Las auditorías globales, reconciliaciones documentales o revisiones arquitectónicas pueden requerir una lectura más amplia cuando el coordinador la indique expresamente.

Si el rol no está claro y el thread no tiene contexto previo del proyecto, el agente debe considerarse principal y realizar la lectura inicial completa.

Debes mantener actualizado structure.md, este markdown se trata de documentar la estructura del proyecto e indicar que hace cada paquete, subpaquete y modulos.
Manten siempre actualizado Fases.md con todas las fases que se ha desarrollado en el proyecto, explicando que incorporo al desarrollo y en donde y por que fue importante implementar esa fase, tambien en ese markdown describe las fases completadas y las fases pendientes por hacer y completar.

Cuando se trabaje con subagentes de IA, el agente principal debe consultar `Subagentes.md` y aplicar su protocolo de delegación. Debe trasladar al prompt del subagente las reglas relevantes; no debe obligarlo a releer el documento completo salvo que su tarea lo requiera expresamente.

## Tecnología obligatoria

- C# y .NET 10 LTS, salvo restricción corporativa documentada.
- WPF y XAML para la interfaz.
- Microsoft.Playwright para .NET.
- Microsoft Edge instalado externamente mediante el canal msedge, usando un contexto temporal de Playwright sin reutilizar perfiles existentes.
- Outlook clásico mediante Microsoft Office Interop/COM.

No utilizar JavaScript, TypeScript, Node.js, Python, Java, PowerShell, Electron, WebView2, Selenium ni Chromium incluido en la aplicación. 
> Hay una excepción con respecto al uso de PowerShell, el agente de IA puede utilizar powershell para automatizar comandos que generan una nueva versión del instalador de la aplicación. Este shell solo es para el uso del usuario, es decir el humano.

HTML, CSS y JavaScript no forman parte de la interfaz propia. Playwright puede inspeccionar el DOM HTML del portal PAM externo porque ese portal es una aplicación web.

## Reglas de trabajo

- Construir por fases pequeñas y revisables.
- No copiar la arquitectura del proyecto anterior.
- No modificar C:\pam automatizacion.
- No utilizar LaunchPersistentContext, userDataDir ni conexión CDP a un perfil existente. Solo los puedes usar para la automatización para del nuevo boton "Acceder al PAM", en cambio para la automatización para solicitar accesos si queda restringida. Para este boton de acceder al PAM puedes utilizar scripts shell si te ayuda a hacer el trabajo más rapido y eficiente, pero el usuario nunca debe ejecutar los shell manualmente, todo debe de ocurrir por detras.
- Las pruebas de integración autorizadas pueden leer credenciales reales desde el almacén protegido; nunca imprimirlas, registrarlas, hardcodearlas ni almacenarlas en texto plano.
- No crear solicitudes PAM ni enviar correos durante pruebas.
- No usar credenciales reales en pruebas unitarias o simulaciones. Pueden utilizarse en pruebas de integración visibles y explícitamente autorizadas contra el entorno PAM permitido.
- Ejecutar compilación y pruebas relacionadas después de cada cambio.
- No afirmar que una tarea está terminada sin evidencia.
- Si documentación, código o portal presentan contradicciones, detenerse y reportarlas.
- Mantener actualizados README.md, LOGIC.md y History.md tras decisiones o cambios relevantes.

## Seguridad

- Usar Windows Credential Manager o DPAPI.
- No guardar contraseñas ni semillas TOTP en texto plano.
- No escribir TOTP, cookies, tokens ni secretos en logs o capturas.
- Permitir navegación únicamente al origen autorizado de PAM.
- Mantener la excepción HTTPS limitada al contexto de PAM.
- No utilizar archivos .env para configuración o secretos.
- Guardar la contraseña y la semilla TOTP en Windows Credential Manager.
- Guardar la configuración privada bajo %LOCALAPPDATA%\PamAutomation\config.
- El ejecutable publicado no debe crear ni mantener resultados, logs, artefactos o temporales operativos persistentes en la PC del usuario.
- Durante desarrollo y pruebas, sí se permite crear `results`, `logs`, `artifacts` y `temp` en la raíz del repositorio para analizar fallos; deben estar excluidos de Git y no contener secretos.
- Proteger con DPAPI CurrentUser el contenido de correo que pueda contener información sensible.
- No escribir configuración dentro de Program Files ni junto al ejecutable.
- Mantener los resultados de ejecución solamente en memoria, salvo una exportación de diagnóstico solicitada explícitamente.
- Usar escrituras atómicas y schemaVersion para permitir migraciones de configuración.

## Validación

La restauración, compilación, ejecución, pruebas y publicación se realizarán desde la terminal con el SDK x64 de .NET 10 disponible en `PATH`. Usar directamente `dotnet restore`, `dotnet build`, el runner integrado de MSTest mediante `dotnet run` y `dotnet publish`; no crear scripts auxiliares para envolver estos comandos. La configuración canónica de desarrollo es `Release | x64`. Las pruebas reales contra PAM y Outlook requieren autorización explícita.
