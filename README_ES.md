# RRCA – Rob's Race Crew Advanced

[English](README.md) · [Deutsch](README_DE.md) · **Español**

RRCA es una extensión gratuita y no oficial para [Crew Chief V4](https://gitlab.com/mr_belowski/CrewChiefV4). Te da un equipo de boxes con tres voces con el que puedes hablar durante la carrera:

- **Ben** observa la carrera y se ocupa de la estrategia.
- **Mik** es tu ingeniero de pista para el coche, los neumáticos y el combustible.
- **Sara** es tu spotter y te avisa de los coches que tienes al lado. Funciona directamente en tu PC y nunca espera a internet.

Pulsas un botón del volante, preguntas con palabras normales y el equipo responde con los datos en directo de tu simulador. Si el simulador no ofrece un dato, el equipo lo dice en lugar de inventarlo.

> **Versión de prueba.** El equipo habla **español, inglés o alemán**, tú eliges. La ventana del programa está en los mismos tres idiomas y se adapta al idioma de Windows. Los mensajes pueden ser incorrectos, llegar tarde o faltar. Solo para simuladores de carreras.

**[Descargar la versión actual](https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/releases/latest)** · La descarga incluye además una guía ilustrada paso a paso: `GUIA_RRCA_ES.pdf`.

---

## Contenido

1. [Qué necesitas](#1-qué-necesitas)
2. [¿Qué son OpenRouter y ElevenLabs?](#2-qué-son-openrouter-y-elevenlabs)
3. [¿Cuánto cuesta?](#3-cuánto-cuesta)
4. [Crear tu clave de OpenRouter](#4-crear-tu-clave-de-openrouter)
5. [Crear tu clave de ElevenLabs](#5-crear-tu-clave-de-elevenlabs)
6. [Instalar RRCA](#6-instalar-rrca)
7. [Configurar RRCA](#7-configurar-rrca)
8. [Tu primera prueba](#8-tu-primera-prueba)
9. [Problemas y soluciones](#9-problemas-y-soluciones)
10. [Datos y privacidad](#10-datos-y-privacidad)
11. [Actualizar, desinstalar e informar de errores](#11-actualizar-desinstalar-e-informar-de-errores)
12. [Detalles técnicos](#12-detalles-técnicos)
13. [Licencia y agradecimientos](#13-licencia-y-agradecimientos)

---

## 1. Qué necesitas

- Un PC con Windows de 64 bits (probado en Windows 11). Windows 10 y 11 suelen traer ya los dos componentes de Microsoft que necesita RRCA: **.NET Framework 4.8** y **Microsoft Edge WebView2 Runtime**. Si falta alguno, RRCA te avisa; descárgalo solo desde microsoft.com.
- Un simulador compatible con Crew Chief, por ejemplo RaceRoom, Automobilista 2, Assetto Corsa Competizione, iRacing, rFactor 2 o Le Mans Ultimate.
- Un **micrófono** (el de los auriculares sirve) y un **botón libre en el volante** para hablar con el equipo.
- **Conexión a internet** mientras conduces.
- Dos cuentas en línea: **OpenRouter** y **ElevenLabs**. Abajo se explican paso a paso.

**No** necesitas saber programar, ni Python, ni SimHub ni nada más.

## 2. ¿Qué son OpenRouter y ElevenLabs?

RRCA es gratuito. Para entender tus preguntas y hablar con voces naturales utiliza dos servicios en línea. Abres una cuenta en cada uno, cargas una pequeña cantidad de saldo y RRCA usa ese saldo. El dinero va a esos servicios, no a nosotros.

| Servicio | Qué hace por ti | Web |
|---|---|---|
| **OpenRouter** | El "cerebro". Convierte tu pregunta hablada en texto y permite que Ben y Mik piensen una respuesta adecuada. OpenRouter es un mercado que da acceso a muchos modelos de IA con una sola cuenta y un solo saldo. | [openrouter.ai](https://openrouter.ai) |
| **ElevenLabs** | Las "voces". Convierte las respuestas del equipo en habla natural, para que Ben, Mik y Sara suenen como personas reales. | [elevenlabs.io](https://elevenlabs.io) |

Cada servicio te da una **clave API**. Es una contraseña larga de letras y números que copias una vez en RRCA para que pueda usar tu cuenta. Trátala como una contraseña: nunca la publiques en un foro ni la muestres en una captura de pantalla.

## 3. ¿Cuánto cuesta?

Precios de **noviembre de 2026**. Los precios cambian; compruébalos en las webs de los proveedores antes de pagar. Se cobra en dólares estadounidenses.

**OpenRouter** (saldo prepago, pagas lo que usas)
- Compras saldo por adelantado, por ejemplo 10 USD. Al pagar con tarjeta, OpenRouter añade una comisión del 5,5 % (mínimo 0,80 USD).
- Para las respuestas por radio, RRCA usa por defecto un modelo rápido y barato, y uno más potente solo para análisis más largos cuando estás parado en boxes. El reconocimiento de voz cuesta unos 0,0045 USD por minuto de voz.
- Estimación aproximada: **una carrera de 30 minutos suele costar unos céntimos, rara vez más de 0,25 USD.** Depende de cuánto hables con el equipo.
- OpenRouter puede caducar el saldo no usado después de un año.

**ElevenLabs** (plan mensual o pago por uso)
- **Free:** 0 USD, unos 10.000 caracteres al mes. Sirve para probar, pero no para correr con regularidad, y no todas las voces se pueden usar en el plan gratuito.
- **Starter:** unos 6 USD al mes (el primer mes costaba 1 USD al escribir esto), unos 75.000 caracteres al mes. **Recomendado para empezar.**
- Hay planes mayores si corres mucho.
- Una sola vez, durante la configuración, RRCA prepara los mensajes estándar del equipo y los avisos de Sara. Eso usa una vez entre 15.000 y 25.000 caracteres aproximadamente.
- Después, una carrera típica usa entre 3.000 y 10.000 caracteres, según lo hablador que configures la radio.

**Consejos para controlar el gasto**
- Pon un **límite de gasto** en cada clave (se explica abajo).
- Empieza con poco, por ejemplo 10 USD en OpenRouter y el plan Starter en ElevenLabs.
- En RRCA, el ajuste **Máximo de consultas de IA por sesión** (por defecto 300) limita cuántas veces pueden Ben y Mik consultar a la IA en una sesión.
- Ambas webs muestran exactamente lo que has gastado.

Estas cifras son estimaciones, no una garantía. Tus costes reales aparecen en tus cuentas.

## 4. Crear tu clave de OpenRouter

1. Entra en [openrouter.ai](https://openrouter.ai) y pulsa **Sign in** (arriba a la derecha). Puedes entrar con una cuenta de Google o con tu correo electrónico.
2. Abre **Credits** ([openrouter.ai/settings/credits](https://openrouter.ai/settings/credits)) y pulsa **Add Credits**. Paga una cantidad pequeña, por ejemplo 10 USD, con tarjeta.
3. Abre **Keys** ([openrouter.ai/settings/keys](https://openrouter.ai/settings/keys)) y pulsa **Create Key**.
   - Nombre: por ejemplo `RRCA`.
   - **Credit limit:** escribe una cantidad, por ejemplo `5`. La clave deja de funcionar cuando se gasta esa cantidad.
4. Pulsa **Create**. La clave se muestra **una sola vez**. Empieza por `sk-or-`. Cópiala enseguida (selecciónala y pulsa Ctrl+C) y guárdala en un lugar seguro, por ejemplo en un gestor de contraseñas.

## 5. Crear tu clave de ElevenLabs

1. Entra en [elevenlabs.io](https://elevenlabs.io) y pulsa **Sign up**. Confirma tu correo electrónico.
2. Elige un plan en **Pricing** o en los ajustes de tu cuenta. **Starter** es la opción segura (ver costes arriba).
3. Elige voces: abre la **Voice Library** (a veces "Voices" → "Explore"). Filtra por tu idioma de radio (por ejemplo **Spanish**) y escucha algunas. Elige tres, una para Ben, otra para Mik y otra para Sara (para Sara va bien una voz femenina), y pulsa **Add to my voices** en cada una. Solo las voces de "My voices" aparecerán después en RRCA.
4. Crea la clave: en tu cuenta abre **API Keys** (normalmente en **Developers** o en el menú del perfil) y pulsa **Create API key**.
   - Nombre: por ejemplo `RRCA`.
   - Permisos: basta con **Text to Speech: Access** y **Voices: Read**. Todo lo demás puede quedar desactivado.
   - Si se ofrece, pon un **credit limit**.
5. Copia la clave (empieza por `sk_`). Normalmente también se muestra **una sola vez**.

## 6. Instalar RRCA

1. Descarga `RobertsCrewChiefAdvanced.zip` desde la [página de descarga](https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/releases/latest).
2. Clic derecho sobre el ZIP → **Extraer todo…** y elige una carpeta nueva, por ejemplo `C:\Simracing\RRCA`. No lo extraigas en una carpeta de Crew Chief existente.
3. En la carpeta nueva, haz doble clic en **CrewChiefV4.exe**. Si Windows muestra el aviso azul "Windows protegió su PC", pulsa **Más información** → **Ejecutar de todas formas**. Aparece porque el programa es nuevo y no está firmado.
4. Se abre la ventana de RRCA. La ventana clásica de Crew Chief queda minimizada detrás, en la barra de tareas; las dos forman parte del mismo programa.

**¿Ya usas Crew Chief?** No hay problema. RRCA tiene sus propios perfiles y sonidos y no modifica tu Crew Chief. No ejecutes los dos a la vez.

## 7. Configurar RRCA

Haz todo esto con el simulador cerrado o parado en boxes, nunca conduciendo.

**a) Elegir idiomas.** Arriba a la derecha, en **Idioma de la interfaz**, cambias la ventana entre Español, English y Deutsch. El idioma del equipo se ajusta aparte en **IA y radio** → **Idioma de la radio** (Automático usa el idioma de Windows). Si cambias el idioma de la radio más tarde, vuelve a pulsar **Preparar clips de radio** y **Generar paquete de Sara** (paso c) y reinicia RRCA.

**b) Introducir las claves.** Ve a **Equipo y voces** → **Servicios y claves**.
- Pega la clave de OpenRouter en el campo **OpenRouter (Ben, Mik, reconocimiento de voz)** (haz clic en el campo y pulsa Ctrl+V) y pulsa **Guardar**.
- Pega la clave de ElevenLabs en el campo **ElevenLabs (voces)** y pulsa **Guardar**.
- Pulsa **Probar conexión**. RRCA indica si la conexión funciona.

**c) Dar voz al equipo.** Sigue en **Equipo y voces**:
1. Pulsa **Cargar lista de voces**. Aparecen tus voces de "My voices" en ElevenLabs.
2. Elige una voz para Ben, Mik y Sara y escúchala con **Probar**.
3. Pulsa **Preparar clips de radio**. Crea una vez los mensajes estándar y tarda unos minutos.
4. Pulsa **Generar paquete de Sara** y espera a que termine.
5. **Cierra RRCA por completo y vuelve a abrirlo.** Los avisos de Sara solo funcionan después de este reinicio.

**d) Micrófono y botón de radio.**
1. En **Equipo y voces** → **Dispositivos y botón de radio**, elige tu micrófono.
2. Pulsa **Ventana de Crew Chief** para traer la ventana clásica al frente.
3. Allí, elige tu volante en la lista de controladores, marca la acción **"KI-Team anfunken"** (el nombre está en alemán: "llamar al equipo de IA") y pulsa **Assign**. Después pulsa el botón del volante que quieras usar. Si no ves la acción, pulsa antes **Add/Remove Actions**.
4. Vuelve a la ventana de RRCA. Puedes volver a minimizar la ventana clásica.

**e) Tu nombre.** En **IA y radio** → **Tu perfil de piloto**, escribe tu nombre y, si quieres, un nombre corto para la radio. Pulsa **Aplicar** abajo.

**f) Tu simulador.** En la página **Cockpit**, activa **Detectar simulador automáticamente**. RRCA pondrá en marcha el equipo cuando tu simulador esté abierto. También puedes elegir el simulador de la lista y pulsar **Iniciar**. En los ajustes del simulador tiene que estar activada la telemetría o la memoria compartida (shared memory); la documentación de Crew Chief lo explica para cada simulador.

## 8. Tu primera prueba

1. Arranca el simulador y siéntate en el coche, en boxes.
2. RRCA debe mostrar el simulador como conectado y el equipo te saluda.
3. Mantén pulsado el botón de radio, haz una pregunta sencilla, por ejemplo *"¿Cuánto combustible tengo?"*, y suelta el botón.
4. Mik debería responder en pocos segundos.
5. Haz después una sesión corta de entrenamiento antes de tu primera carrera.

## 9. Problemas y soluciones

**El equipo no dice nada.**
Comprueba que RRCA muestra el simulador como conectado, que la telemetría está activada en el simulador y que **Probar conexión** funcionó con tus claves. Revisa tu saldo en OpenRouter y ElevenLabs.

**Sara no me avisa.**
Vuelve a pulsar **Generar paquete de Sara**, espera a que termine y reinicia RRCA. Un aviso rojo en la ventana explica qué falta.

**La lista de voces está vacía.**
Añade voces a **My voices** en ElevenLabs y pulsa de nuevo **Cargar lista de voces**. Comprueba que la clave tiene el permiso **Voices: Read**.

**No entiende mi pregunta.**
Mantén pulsado el botón un momento antes de hablar y suéltalo cuando hayas terminado. Habla en tu idioma de radio. Comprueba el micrófono elegido.

**De repente deja de hablar.**
Puede que se haya acabado tu saldo o tus caracteres del mes, o que se haya alcanzado el límite de la clave. Revisa las dos webs.

**Los comandos de voz clásicos de Crew Chief en inglés no funcionan.**
Estos comandos antiguos usan el reconocimiento de voz de Windows. Instala el inglés con reconocimiento de voz en Windows (Configuración → Hora e idioma → Idioma y región). Ben, Mik y Sara no lo necesitan.

## 10. Datos y privacidad

- Cuando hablas con el equipo, tu pregunta hablada, los datos de carrera necesarios y, si la memoria del piloto está activada, las notas relacionadas se envían a OpenRouter y al modelo de IA que use. El texto que se va a decir se envía a ElevenLabs. Se aplican las normas de cada proveedor: [privacidad de OpenRouter](https://openrouter.ai/privacy), [condiciones de ElevenLabs](https://elevenlabs.io/terms-of-use-eu).
- El micrófono solo graba mientras mantienes pulsado el botón de radio. No se guardan grabaciones completas.
- Al iniciarse, RRCA pregunta a GitHub si hay una versión nueva. No se envían datos personales.
- Tus claves se guardan cifradas para tu usuario de Windows (Windows DPAPI) y nunca se incluyen en la descarga.
- La opción **Enviar nombres de pilotos a la IA** está desactivada en los perfiles nuevos.
- La memoria del piloto guarda notas y preferencias solo en tu PC. No entrena ningún modelo de IA.

## 11. Actualizar, desinstalar e informar de errores

- **Actualizar:** cuando hay una versión nueva, RRCA muestra arriba un botón **Actualizar a …**. Púlsalo y confirma: RRCA descarga y comprueba la actualización, se reinicia y conserva tus claves, perfiles y memoria. También puedes descargar el ZIP nuevo y extraerlo encima de tu carpeta de RRCA con RRCA cerrado.
- **No** instales el instalador normal de Crew Chief ni sus actualizaciones sobre la carpeta de RRCA. Si tienes un Crew Chief normal, déjalo en su propia carpeta.
- **Desinstalar:** cierra RRCA y borra la carpeta de RRCA. Tus datos personales (ver la tabla de abajo) se quedan hasta que los borres tú.
- **Informar de un error:** abre un [issue en GitHub](https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/issues) o escribe en el [hilo de virtualracing.org](https://forum.virtualracing.org/threads/rrca-erweiterung-freier-ki-boxenfunk-fuer-crew-chief-v4.146346/). Indica la versión (`VERSION.txt` en la carpeta de RRCA), el simulador, el circuito, la hora y lo que oíste. **Nunca publiques tus claves.** Por favor, no contactes al equipo de Crew Chief por temas de RRCA.

## 12. Detalles técnicos

### Dónde guarda RRCA los datos

"Documentos" es tu carpeta Documentos de Windows (puede estar redirigida a OneDrive). Cada carpeta se crea solo cuando se usa la función correspondiente.

| Qué | Dónde |
|---|---|
| Perfiles | `Documentos\RRCA\CrewChiefV4\Profiles` |
| Ajustes de controladores | `…\Profiles\ControllerData` |
| Registros | `Documentos\RRCA\CrewChiefV4\DebugLogs` |
| Registro de radio IA, si está activado | `Documentos\RRCA\CrewChiefV4\AiTeam\Logs` |
| Datos de ejecución y sonidos | `%LOCALAPPDATA%\RobertsCrewChiefAdvanced` y `…\Sounds` |
| Paquete de spotter de Sara | `…\Sounds\voice\spotter_Sara` |
| Ventana de RRCA (idioma, caché) | `%LOCALAPPDATA%\RCCA\WebView2` |
| Ajustes de .NET | `%LOCALAPPDATA%\Britton_IT_Ltd\…\user.config` |
| Claves | `%APPDATA%\RobertsCrewChiefAdvanced\api_key.bin` y `elevenlabs_key.bin` |
| Clips de voz preparados | `%APPDATA%\RobertsCrewChiefAdvanced\stimmen-cache` |
| Memoria del piloto, si está activada | `Documentos\RRCA\Fahrergedaechtnis` |
| Grabador de diagnóstico, si está activado | `Documentos\CrewChief20\RCCA-LiveDiagnostics` |

El grabador de diagnóstico está limitado a 128 MB por grabación y 512 MB en total. Nunca guarda claves ni grabaciones del micrófono, pero puede contener nombres y textos de radio; revísalo antes de compartirlo.

### Compilarlo tú mismo

Con Visual Studio 2022 o las Build Tools:

```
msbuild CrewChiefV4/CrewChiefV4.csproj -restore -p:Configuration=Release
./build/package.ps1 -OutDir dist/RRCA -PublicDistribution
```

## 13. Licencia y agradecimientos

RRCA es una extensión no oficial basada en Crew Chief V4 de Britton IT Ltd y, como Crew Chief, se publica bajo la [licencia MIT](LICENSE). Muchas gracias al equipo de Crew Chief por esta base. RRCA no es un producto del equipo de Crew Chief ni cuenta con su soporte. Los paquetes de sonido de Crew Chief tienen sus propios derechos y no se incluyen. Otros componentes: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), [NOTICES_ES.md](NOTICES_ES.md).

Un proyecto de Robert y Joshua Gerke.
