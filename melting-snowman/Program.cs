using System.Collections.Concurrent;
using FluentValidation;
using MeltingSnowman.Logic;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() {Title = "Melting-Snowman", Version = "v1"});
});
builder.Services.AddScoped<IValidator<string>, GuessValidator>();
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

var previousGuesses  = new ConcurrentDictionary<int,int>();
int nextGameId = 0;
var games = new ConcurrentDictionary<int,MeltingSnowmanGame>();

app.MapGet("/game/{id}", (int id) => 
{
    if(id >= nextGameId){
        return Results.BadRequest();
    }
    var game = games[id];
    return Results.Ok(new GetGameResponse(game.Word, previousGuesses[id]));
}).Produces<GetGameResponse>(StatusCodes.Status200OK)
.Produces<GetGameResponse>(StatusCodes.Status400BadRequest).WithOpenApi(o =>
{
    o.Description = "Returns the status of the game";
    o.Responses[((int)StatusCodes.Status200OK).ToString()].Description = "Successful.";
    o.Responses[((int)StatusCodes.Status400BadRequest).ToString()].Description = "Not Successful.";
    return o;
});

app.MapPost("/game/{id}", (int id, string letter, IValidator<string> validator) => 
{
    if(id >= nextGameId){
        return Results.BadRequest();
    }
    var vailidationResult = validator.Validate(letter);
    if (!vailidationResult.IsValid){
        return Results.ValidationProblem(vailidationResult.ToDictionary());
    }
    int occurences = games[id].Guess(letter);
    previousGuesses[id]++;
    return Results.Ok(new PostGameResponse(occurences, games[id].Word, previousGuesses[id]));
}).Produces<GetGameResponse>(StatusCodes.Status200OK)
.Produces<GetGameResponse>(StatusCodes.Status400BadRequest).WithOpenApi(o =>
{
    o.Description = "Plays a round.";
    o.Responses[((int)StatusCodes.Status200OK).ToString()].Description = "Successful.";
    o.Responses[((int)StatusCodes.Status400BadRequest).ToString()].Description = "Not Successful.";
    return o;
});

app.MapPost("/game", () => {
    int gameId = nextGameId;
    previousGuesses.TryAdd(gameId,0);
    games.TryAdd(gameId, new MeltingSnowmanGame());
    nextGameId++;   
    return gameId;
}).Produces<int>(StatusCodes.Status200OK).WithOpenApi(o =>
{
    o.Description = "Creates a game.";
    o.Responses[((int)StatusCodes.Status200OK).ToString()].Description = "Successful.";
    return o;
});


app.Run();

record GetGameResponse(string WordToGuess, int NumberOfGuesses);
record PostGameResponse(int Occurences, string WordToGuess, int NumberOfGuesses);

class GuessValidator: AbstractValidator<string>
{
    public GuessValidator(){
        RuleFor(c => c).NotNull().Length(1,1);
    }
}