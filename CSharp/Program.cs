using PEG;

try {
    using StreamReader reader = new("PEG.grammar");
    var parser = new Parser(reader.ReadToEnd());
    if (parser.TryParse(out var rules)) {
        foreach (var rule in rules) {
            Console.WriteLine(rule);
        }
    } else {
        Console.WriteLine("parsing failed");
    }
} catch (IOException e) {
    Console.WriteLine(e.Message);
}
