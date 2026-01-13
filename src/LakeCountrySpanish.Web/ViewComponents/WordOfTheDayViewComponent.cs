using Microsoft.AspNetCore.Mvc;

namespace LakeCountrySpanish.Web.ViewComponents;

public class WordOfTheDayViewComponent : ViewComponent
{
    private static readonly List<SpanishWord> Words = new()
    {
        new SpanishWord("Hola", "Hello", "oh-la", "Hola, ¿cómo estás?", "Hello, how are you?"),
        new SpanishWord("Gracias", "Thank you", "grah-see-ahs", "Muchas gracias por tu ayuda.", "Thank you very much for your help."),
        new SpanishWord("Amigo", "Friend", "ah-mee-go", "Mi mejor amigo vive en Perú.", "My best friend lives in Peru."),
        new SpanishWord("Feliz", "Happy", "feh-lees", "Estoy muy feliz hoy.", "I am very happy today."),
        new SpanishWord("Aprender", "To learn", "ah-pren-der", "Me encanta aprender español.", "I love to learn Spanish."),
        new SpanishWord("Familia", "Family", "fah-mee-lee-ah", "Mi familia es muy grande.", "My family is very big."),
        new SpanishWord("Comida", "Food", "koh-mee-dah", "La comida peruana es deliciosa.", "Peruvian food is delicious."),
        new SpanishWord("Viaje", "Trip/Journey", "vee-ah-heh", "Quiero hacer un viaje a Machu Picchu.", "I want to take a trip to Machu Picchu."),
        new SpanishWord("Bonito", "Beautiful/Pretty", "boh-nee-toh", "El paisaje es muy bonito.", "The landscape is very beautiful."),
        new SpanishWord("Corazón", "Heart", "koh-rah-sohn", "Te quiero con todo mi corazón.", "I love you with all my heart."),
        new SpanishWord("Sueño", "Dream", "swen-yo", "Mi sueño es hablar español perfectamente.", "My dream is to speak Spanish perfectly."),
        new SpanishWord("Aventura", "Adventure", "ah-ven-too-rah", "Cada clase es una nueva aventura.", "Every class is a new adventure."),
        new SpanishWord("Llama", "Llama", "yah-mah", "La llama es un animal de los Andes.", "The llama is an animal from the Andes."),
        new SpanishWord("Montaña", "Mountain", "mon-tahn-yah", "Los Andes son las montañas más largas.", "The Andes are the longest mountains."),
        new SpanishWord("Sol", "Sun", "sohl", "El sol brilla todos los días.", "The sun shines every day."),
        new SpanishWord("Luna", "Moon", "loo-nah", "La luna está llena esta noche.", "The moon is full tonight."),
        new SpanishWord("Estrella", "Star", "es-trey-yah", "Las estrellas brillan en el cielo.", "The stars shine in the sky."),
        new SpanishWord("Agua", "Water", "ah-gwah", "Necesito beber agua.", "I need to drink water."),
        new SpanishWord("Café", "Coffee", "kah-feh", "El café peruano es excelente.", "Peruvian coffee is excellent."),
        new SpanishWord("Música", "Music", "moo-see-kah", "Me gusta escuchar música latina.", "I like to listen to Latin music."),
        new SpanishWord("Bailar", "To dance", "bai-lar", "Vamos a bailar salsa.", "Let's dance salsa."),
        new SpanishWord("Cariño", "Affection/Darling", "kah-reen-yo", "Te tengo mucho cariño.", "I have a lot of affection for you."),
        new SpanishWord("Amistad", "Friendship", "ah-mees-tahd", "La amistad es un tesoro.", "Friendship is a treasure."),
        new SpanishWord("Cielo", "Sky/Heaven", "see-eh-lo", "El cielo está muy azul hoy.", "The sky is very blue today."),
        new SpanishWord("Flor", "Flower", "flohr", "Esta flor es muy hermosa.", "This flower is very beautiful."),
        new SpanishWord("Mariposa", "Butterfly", "mah-ree-poh-sah", "La mariposa vuela sobre las flores.", "The butterfly flies over the flowers."),
        new SpanishWord("Arcoíris", "Rainbow", "ar-koh-ee-rees", "Mira el arcoíris después de la lluvia.", "Look at the rainbow after the rain."),
        new SpanishWord("Esperanza", "Hope", "es-peh-rahn-sah", "Nunca pierdas la esperanza.", "Never lose hope."),
        new SpanishWord("Alegría", "Joy", "ah-leh-gree-ah", "La alegría de aprender es especial.", "The joy of learning is special."),
        new SpanishWord("Abrazo", "Hug", "ah-brah-so", "Te mando un abrazo grande.", "I send you a big hug.")
    };

    public IViewComponentResult Invoke()
    {
        // Get consistent word based on day of year
        var dayOfYear = DateTime.UtcNow.DayOfYear;
        var wordIndex = dayOfYear % Words.Count;
        var word = Words[wordIndex];

        return View(word);
    }
}

public class SpanishWord
{
    public string Spanish { get; }
    public string English { get; }
    public string Pronunciation { get; }
    public string ExampleSpanish { get; }
    public string ExampleEnglish { get; }

    public SpanishWord(string spanish, string english, string pronunciation, string exampleSpanish, string exampleEnglish)
    {
        Spanish = spanish;
        English = english;
        Pronunciation = pronunciation;
        ExampleSpanish = exampleSpanish;
        ExampleEnglish = exampleEnglish;
    }
}
