# Bitácora de Sesión con el Agente — Asignación 1

**Estudiante:** Eury Cuevas  
**Proyecto:** Biblioteca con Préstamos  
**Fecha:** 27 de septiembre de 2026  

---

## 1. Tarea delegada: Creación de las 4 entidades base del dominio de Negocio
* **Qué le pedí:** Que me generara los archivos en C# de las cuatro entidades obligatorias para mi módulo de biblioteca (`Recurso`, `Lector`, `Prestamo` y `Multa`) manteniendo las relaciones necesarias y el principio de responsabilidad única.
* **Qué me devolvió:** Me generó las cuatro clases con sus propiedades básicas y namespaces adecuados, pero inicialmente colocó todas las clases compartiendo un mismo archivo de manera plana sin separarlas en archivos individuales, y omitió la propiedad de estado en la entidad `Prestamo` que es vital para la máquina de estados posterior.
* **Caso en que se equivocó:** 
  * *Error detectado:* El agente agrupó todas las clases en un solo bloque de código en lugar de crear archivos separados por clase, y olvidó incluir los atributos necesarios para el control de estados en `Prestamo`.
  * *Cómo lo detecté:* Al revisar los requerimientos de la tarea y la estructura de archivos que exigen clases independientes orientadas a componentes.
  * *Cómo lo corregí:* Le indiqué explícitamente que debía separar cada entidad en su propio archivo físico (`Recurso.cs`, `Lector.cs`, `Prestamo.cs`, `Multa.cs`) y añadir la propiedad `Estado` para la máquina de estados del préstamo.

---

## 2. Tarea delegada: Generación del Diagrama de Componentes C4 en el README
* **Qué le pedí:** Que redactara el diagrama de componentes en el `README.md` utilizando Mermaid siguiendo la especificación del modelo C4 nivel 3 (separando el Core del Módulo de Negocio y etiquetando correctamente las flechas).
* **Qué me devolvió:** Un diagrama de flujo en Mermaid que conectaba el módulo de negocio con el Core y añadía las relaciones de auditoría con flechas punteadas.
* **Caso en que se equivocó:**
  * *Error detectado:* En la primera iteración, el agente puso una flecha directa desde el Core hacia el módulo de negocio para notificaciones, lo cual violaba el requisito fundamental de diseño (RD-03: *El Core no depende del módulo de negocio; la dependencia va en un solo sentido*).
  * *Cómo lo detecté:* Al validar el diagrama contra la regla de la frontera Core / Módulo de Negocio explicada en la diapositiva 8 de la PPT ("El Core no conoce las entidades del negocio").
  * *Cómo lo corregí:* Le ordené corregir la dirección de la dependencia, asegurando que sea el módulo de negocio el que invoque o dispare las notificaciones hacia el Core, y nunca al revés.
