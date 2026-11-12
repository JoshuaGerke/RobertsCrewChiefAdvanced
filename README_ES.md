# Equipo de IA para Crew Chief

Guía ilustrada paso a paso: `GUIA_RRCA_ES.pdf` en la misma carpeta.

## Qué es esta aplicación

RRCA es un proyecto personal no oficial, gratuito y no comercial, basado en Crew Chief V4 de Britton IT Ltd (licencia MIT). Ben se ocupa de estrategia y observación de carrera, Mik del coche y las preguntas técnicas, y Sara de avisos laterales locales inmediatos. La ventana clásica de Crew Chief forma parte de la misma aplicación modificada y puede permanecer minimizada. No ejecutes otro Crew Chief para el mismo simulador.

## Antes de empezar

El software alfa puede emitir mensajes incorrectos, tardíos o ausentes. Solo para simulación. Esta actualización tiene pruebas automáticas; faltan la comprobación física del audio y la prueba en pista. Que la aplicación sea gratuita no significa que los servicios en la nube lo sean.

## Requisitos

1. Windows de 64 bits, .NET Framework 4.8 y Microsoft Edge WebView2 Runtime.
2. Un simulador compatible con memoria compartida, UDP o el plugin necesario activado.
3. Cuenta y clave propias de OpenRouter para conversaciones y transcripción.
4. Cuenta y clave propias de ElevenLabs, voces autorizadas y cuota suficiente.
5. Micrófono, salida de audio y un botón accesible para hablar por radio.

## 1 - Extraer e iniciar

Extrae el ZIP completo del candidato Freeware en una carpeta nueva con permiso de escritura. No sobrescribas la instalación original de Crew Chief. Inicia CrewChiefV4.exe desde esta carpeta. El modo público usa perfiles y sonidos separados; puede reutilizar claves y memoria de RRCA del mismo usuario de Windows guardadas fuera del ZIP. Si faltan sonidos base, usa el gestor de sonidos integrado. Los paquetes de audio no se incluyen.

## 2 - Claves, voces y micrófono

En Equipo y voces, abre Servicios y claves. Guarda por separado tus claves de OpenRouter y ElevenLabs y prueba la conexión. Carga la lista de voces, elige una voz apta para alemán para cada miembro y escúchala con el coche parado. Prepara los clips de radio y genera el paquete de spotter de Sara. Cierra completamente la aplicación y vuelve a abrirla cuando termine correctamente. Sigue los avisos de preparación y selecciona el micrófono correcto. No se añade una voz de Windows como sustituta automática.

## 3 - Asignar el botón de radio

Abre la ventana de Crew Chief. Selecciona el controlador, marca la acción KI-Team anfunken, pulsa Assign y presiona el botón elegido. Usa Add/Remove Actions si no aparece. Mantén pulsado, habla y suelta. Una prueba de voz no comprueba el micrófono ni la conexión de IA: pruébalos por separado en parado.

## 4 - Piloto, modelos y simulador

En IA y radio, introduce tu nombre completo y apodo y aplica con el equipo detenido. Conserva inicialmente los modelos preconfigurados y comprueba su disponibilidad con el proveedor. Selecciona el simulador e inicia o activa la detección automática. Detectar un proceso no demuestra que exista telemetría válida. Prueba primero una pregunta en parado y luego una sesión corta de entrenamientos.

## 5 - Volumen de cada miembro

En el cockpit hay un control de 0-100 % debajo de Ben, Mik y Sara. El 100 % inicial conserva el nivel anterior. El nivel individual multiplica el volumen general Messages; Sara conserva además el refuerzo de spotter existente. Se aplica a nuevos mensajes y pruebas, se guarda en el perfil activo y no requiere reinicio ni regenerar clips. El 0 % silencia esa voz, incluidos los avisos de Sara. Background regula el ambiente de radio, no el juego. Los interruptores de activar miembros siguen siendo independientes.

## 6 - Idioma, ayuda e informes periódicos

Elige Deutsch, English o Español arriba para la nueva interfaz y sus ayudas de interrogación. La radio hablada y los clips de Sara siguen actualmente en alemán; cambiar la interfaz no los traduce. Diez temas periódicos complementan la detección independiente de eventos. Cada tema tiene activación, intervalo y tolerancia aleatoria antes/después. 120 -7/+8 planifica entre 113 y 128 segundos; avisos urgentes, carga del piloto y datos ausentes pueden aplazarlo. No son horas de emisión garantizadas.

## Privacidad y datos

El envío de nombres de rivales está desactivado inicialmente. Activarlo permite incluirlos en consultas de IA; tu apodo, preguntas y texto hablado pueden contener nombres igualmente. La opción ahora mantiene su estado durante las actualizaciones de pantalla. Preguntas, hechos relevantes y recuerdos seleccionados pueden ir al proveedor de IA y al proveedor del modelo. Los fragmentos del botón de radio van a transcripción; el texto hablado va al proveedor de voz. No se guarda una grabación continua o completa del micrófono. Las claves se protegen con Windows DPAPI para tu usuario. No compartas claves, perfiles, cachés de voz ni registros privados.

## Memoria y diagnóstico

La memoria persistente opcional guarda texto, preferencias y hechos limitados fuera del ZIP. No entrena pesos del modelo ni cambia código. Las notas relevantes pueden acompañar las conversaciones. El grabador JSONL local es opcional: hasta 20 Hz, máximo 128 MiB por grabación y 512 MiB en total. Los registros pueden contener nombres y conversaciones; revísalos antes de compartir.

## Actualizaciones, soporte y derechos

Mantén y actualiza por separado el Crew Chief original. Nunca instales su actualización binaria sobre RRCA; usa un paquete RRCA probado. El modo público bloquea ese actualizador. Los datos permanecen fuera del ZIP; eliminar la aplicación no elimina automáticamente tus datos. Informa al repositorio RRCA indicando versión/commit, simulador, circuito, sesión, hora, mensaje esperado/escuchado y un extracto revisado. No se garantiza un plazo de respuesta. Gracias a los desarrolladores de Crew Chief. La licencia MIT del código no concede derechos sobre voces, grabaciones o marcas; no se afirma un aval personal de los desarrolladores. Es un candidato de revisión interna: quedan cuestiones de publicación en FREEWARE_PRUEFUNG.md. Consulta también los avisos de terceros incluidos.

**Comandos de voz:** RRCA no incluye a propósito `Microsoft.Speech.dll` de Microsoft. Por eso, los comandos de voz clásicos de Crew Chief usan el reconocimiento de voz de Windows. Windows necesita el idioma correspondiente con reconocimiento de voz instalado (Configuración → Hora e idioma → Idioma y región; inglés para los comandos en inglés de Crew Chief). El equipo de IA (Ben, Mik, Sara) no depende de esto: escucha mediante su propio botón de radio y el reconocimiento de voz en la nube.
