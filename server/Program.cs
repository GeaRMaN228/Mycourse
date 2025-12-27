using System;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

internal class Program{
    private static void Main(string[] args){
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddAuthorization();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // будет ли валидироваться издатель при созании токена
                    ValidateIssuer = true,
                    // Издатель
                    ValidIssuer = AuthOptions.ISSUER,
                    // будет ли валидироваться потребитель токена
                    ValidateAudience = true,
                    // Потребитель
                    ValidAudience = AuthOptions.AUDIENCE,
                    // будет ли валидироваться время существования токена
                    ValidateLifetime = true,
                    // ключ безопасности
                    IssuerSigningKey = AuthOptions.GetSymmetricSecurityKey(),
                    // валидация ключа безопасности
                    ValidateIssuerSigningKey = true,
                };
            });
        
        var app = builder.Build();
        
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseAuthorization();
        app.UseAuthentication();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        // КОННЕКТИМСЯ К БД
        SQLitePCL.Batteries.Init();// без этого не подключиться к БД
        string path = "C:/MyCourse/server/server/gronsfeld.db";
        DBManager database = new DBManager();
        database.ConnectToDB(path);
        database.CreateTables();

        var ans = new Answer_Request();

        // Пошли эндпоинты
        app.MapPost("/encrypt/text/{text_id}", ([FromBody] Answer_Request data, int text_id) =>
        {
            if(database.ValidateToken(data.Token))
            {
                GronsfeldCipher Cypher = new GronsfeldCipher();
                if (!Cypher.ValidateKey(data.Key))
                {
                    return Results.Json(ans.MakeJson("Ключ введён не верно"));
                }
                string text = database.GetText(data.Token, text_id);
                var answer = ans.MakeJson("Текст зашифрован",text:Cypher.Encrypt(text, data.Key));
                if(database.UpdateText(data.Token, text_id, Cypher.Encrypt(text, data.Key)))
                {
                    database.AddLogEncryptText(data.Token);
                    return Results.Json(answer);
                }else return Results.Json(ans.MakeJson("Текст не найден"));
            }else return Results.Unauthorized();
        });

        app.MapPost("/encrypt", ([FromBody] Answer_Request data) =>
        {
            if (database.ValidateToken(data.Token))
            {
                GronsfeldCipher Cypher = new GronsfeldCipher();
                if (!Cypher.ValidateKey(data.Key))
                {
                    return Results.Json(ans.MakeJson("Ключ введён не верно"));
                }
                var answer = ans.MakeJson("Текст зашифрован",text:Cypher.Encrypt(data.Text, data.Key));
                database.AddLogEncrypt(data.Token);
                return Results.Json(answer);
            }else return Results.Unauthorized();
        });

        app.MapPost("/decrypt/text/{text_id}", ([FromBody] Answer_Request data, int text_id) =>
        {
            if(database.ValidateToken(data.Token))
            {
                GronsfeldCipher Cypher = new GronsfeldCipher();
                if (!Cypher.ValidateKey(data.Key))
                {
                    return Results.Json(ans.MakeJson("Ключ введён не верно"));
                }
                string text = database.GetText(data.Token, text_id);
                var answer = ans.MakeJson("Текст расшифрован",text:Cypher.Decrypt(text, data.Key));
                if(database.UpdateText(data.Token, text_id, Cypher.Decrypt(text, data.Key)))
                {
                    database.AddLogDecryptText(data.Token);
                    return Results.Json(answer);
                }else return Results.Json(ans.MakeJson("Текст не найден"));
            }else return Results.Unauthorized();
        });

        app.MapPost("/decrypt", ([FromBody] Answer_Request data) =>
        {
            if (database.ValidateToken(data.Token))
            {
                GronsfeldCipher Cypher = new GronsfeldCipher();
                if (!Cypher.ValidateKey(data.Key))
                {
                    return Results.Json(ans.MakeJson("Ключ введён не верно"));
                }
                var answer = ans.MakeJson("Текст расшифрован",text:Cypher.Decrypt(data.Text, data.Key));
                database.AddLogDecrypt(data.Token);
                return Results.Json(answer);
            }else return Results.Unauthorized();
        });

        app.MapPost("/login", ([FromBody] Answer_Request data) =>
        {
            string user = data.Login;
            string password = data.Password;

            if(database.Login(user, password))
            {
                var jwt = new JwtSecurityToken(
                    issuer: AuthOptions.ISSUER,
                    audience: AuthOptions.AUDIENCE,
                    expires: DateTime.UtcNow.AddMinutes(30),
                    signingCredentials: new SigningCredentials(AuthOptions.GetSymmetricSecurityKey(), SecurityAlgorithms.HmacSha256));
                var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);
                
                database.AddToken(encodedJwt, user);
                return Results.Json(ans.MakeJson("Вход выполнен", encodedJwt));
            }else{
                return Results.Json(ans.MakeJson("Логин или пароль не верный"));
            }
        });

        app.MapPost("/register", ([FromBody] Answer_Request data) =>
        {
            string user = data.Login;
            string password = data.Password;
            if (!database.UserExist(user))
            {
                database.AddUser(user, password);
                return Results.Json(ans.MakeJson("Пользователь зарегистрирован"));
            }else return Results.Json(ans.MakeJson("Пользователь с таким именем уже существует"));
        });

        app.MapPatch("/update/password", ([FromBody] Answer_Request data) =>
        {
            string token = data.Token;
            string new_pass = data.Password;
            if (database.ValidateToken(token))
            {
                database.UpdatePassword(token, new_pass);
                var jwt = new JwtSecurityToken(
                    issuer: AuthOptions.ISSUER,
                    audience: AuthOptions.AUDIENCE,
                    expires: DateTime.UtcNow.AddMinutes(30),
                    signingCredentials: new SigningCredentials(AuthOptions.GetSymmetricSecurityKey(), SecurityAlgorithms.HmacSha256));
                var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);
                database.AddToken(encodedJwt);
                return Results.Json(ans.MakeJson("Пароль изменён", token: encodedJwt));
            }else return Results.Unauthorized();
        });

        app.MapGet("/get/logs", (HttpRequest request) =>
        {
            if (!request.Headers.TryGetValue("Authorization", out var authHeader))
                return Results.Unauthorized();
            string token = authHeader.ToString().Substring("Bearer ".Length-1).Trim();

            if (database.ValidateToken(token))
            {
                Dictionary<int, string> logs = database.GetLogs(token);

                return Results.Json(logs);
            }else return Results.Unauthorized();
        });

        app.MapGet("/get/random_key", (HttpRequest request) =>
        {
            if (!request.Headers.TryGetValue("Authorization", out var authHeader))
                return Results.Unauthorized();
            string token = authHeader.ToString().Substring("Bearer ".Length-1).Trim();

            if (database.ValidateToken(token))
            {
                Random rnd = new Random(DateTime.UtcNow.Millisecond);
                database.AddLogKeyGen(token);
                return Results.Json(ans.MakeJson("Ключ сгенерирован",key: rnd.Next(0, Int32.MaxValue)));
            }else return Results.Unauthorized();
        });

        app.MapGet("/get/text/{text_id}", (HttpRequest request, int text_id) =>
        {
            if (!request.Headers.TryGetValue("Authorization", out var authHeader))
                return Results.Unauthorized();
            string token = authHeader.ToString().Substring("Bearer ".Length-1).Trim();

            if (database.ValidateToken(token))
            {
                string text = database.GetText(token,text_id);
                if(text == null)
                {
                    return Results.Json(ans.MakeJson("Текст не найден"));
                }
                return Results.Json(ans.MakeJson("Текст найден", text: text));
            }else return Results.Unauthorized();
        });

        app.MapGet("/get/texts", (HttpRequest request) =>
        {
            if (!request.Headers.TryGetValue("Authorization", out var authHeader))
                return Results.Unauthorized();
            string token = authHeader.ToString().Substring("Bearer ".Length-1).Trim();

            if (database.ValidateToken(token))
            {
                return Results.Json(database.GetTexts(token));
            }else return Results.Unauthorized();
        });

        app.MapDelete("/delete/text/{text_id}", (HttpRequest request, int text_id) =>
        {
            if (!request.Headers.TryGetValue("Authorization", out var authHeader))
                return Results.Unauthorized();
            string token = authHeader.ToString().Substring("Bearer ".Length-1).Trim();

            if (database.ValidateToken(token))
            {
                database.DeleteText(token, text_id);
                return Results.Json(ans.MakeJson("Текст успешно удалён"));
            }else return Results.Unauthorized();
        });

        app.MapPatch("/update/text/{text_id}", ([FromBody] Answer_Request data, int text_id) =>
        {
            string token = data.Token;
            if (database.ValidateToken(token))
            {
                if(database.UpdateText(token, text_id, data.Text))
                {
                    return Results.Json(ans.MakeJson("Текст успешно обновлён"));
                }else return Results.Json(ans.MakeJson("Текст не найден"));
            }else return Results.Unauthorized();
        });

        app.MapPost("/add/text", ([FromBody] Answer_Request data) =>
        {
            string token = data.Token;
            if (database.ValidateToken(token))
            {
                database.AddText(token, data.Text);
                return Results.Ok();
            }else return Results.Unauthorized();
        });

        app.MapPost("/logout", ([FromBody] Answer_Request data) =>
        {
            string token = data.Token;
            if (database.ValidateToken(token))
            {
                int us_id = database.GetUserId(token: token);
                database.LogoutLog(token);
                database.DeleteOtherTokens(us_id);
                return Results.Ok();
            }else return Results.Unauthorized();
        });

        app.Run();
        database.Disconnect();
    }
}

public class Answer_Request
{
    public string? Status{ get; set; }
    public string? Token{ get; set; }
    public string? Text{ get; set; }
    public int Key{ get; set; }
    public string? Login{ get; set; }
    public string? Password{ get; set; }

    public Answer_Request MakeJson(string status = null, string token = null, string text = null, int key = -1, string login = null, string password = null)
    {
        var answer = new Answer_Request
        {
            Status = status,
            Token = token,
            Text = text,
            Key = key,
            Login = login,
            Password = password
        };
        return answer;
    }
}

public class AuthOptions
{
    public const string ISSUER = "GronsfeldServer";
    public const string AUDIENCE = "GronsfeldClient";
    const string KEY = "pomidorki_ya_lybly.fuck_ni99ers!!";
    public static SymmetricSecurityKey GetSymmetricSecurityKey() =>
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(KEY));
}

public class GronsfeldCipher
{
    private const string RussianAlphabet = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ";
    private const string EnglishAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public string Encrypt(string text, int key) //шифрование
    {
        if (string.IsNullOrEmpty(text))
            return text;
        
        StringBuilder result = new StringBuilder();
        string keyStr = key.ToString();
        int keyIndex = 0;
        
        foreach (char c in text)
        {
            if (char.IsLetter(c))
            {
                int shift = keyStr[keyIndex % keyStr.Length] - '0';
                if (IsRussianLetter(c))
                {
                    char encryptedChar = ShiftChar(c, shift, RussianAlphabet);
                    result.Append(encryptedChar);
                }
                else if (IsEnglishLetter(c))
                {
                    char encryptedChar = ShiftChar(c, shift, EnglishAlphabet);
                    result.Append(encryptedChar);
                }
                else
                {
                    result.Append(c);
                    continue;
                }
            }
            else
            {
                result.Append(c);
            }
            keyIndex++;
        }
        
        return result.ToString();
    }
    
    public string Decrypt(string text, int key)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        
        StringBuilder result = new StringBuilder();
        string keyStr = key.ToString();
        int keyIndex = 0;
        
        foreach (char c in text)
        {
            if (char.IsLetter(c))
            {
                int shift = keyStr[keyIndex % keyStr.Length] - '0';

                if (IsRussianLetter(c))
                {
                    char decryptedChar = ShiftChar(c, -shift, RussianAlphabet);
                    result.Append(decryptedChar);
                }
                else if (IsEnglishLetter(c))
                {
                    char decryptedChar = ShiftChar(c, -shift, EnglishAlphabet);
                    result.Append(decryptedChar);
                }
                else
                {
                    result.Append(c);
                    continue;
                }  
            }
            else
            {
                result.Append(c);
            }
            keyIndex++;
        }
        
        return result.ToString();
    }
    // сдвиг символа
    private char ShiftChar(char c, int shift, string alphabet)
    {
        bool isUpper = char.IsUpper(c);
        string workingAlphabet = isUpper ? alphabet : alphabet.ToLower();
        
        int index = workingAlphabet.IndexOf(c);
        if (index < 0)
            return c;
        // обрабатываем отрицательный сдвиг для дешифрования
        if (shift < 0)
        {
            shift = alphabet.Length + (shift % alphabet.Length);
        }
        
        int newIndex = (index + shift) % alphabet.Length;
        return workingAlphabet[newIndex];
    }
    
    private bool IsRussianLetter(char c)
    {
        char upperC = char.ToUpper(c);
        return RussianAlphabet.Contains(upperC) || RussianAlphabet.ToLower().Contains(c);
    }
    
    private bool IsEnglishLetter(char c)
    {
        char upperC = char.ToUpper(c);
        return EnglishAlphabet.Contains(upperC) || EnglishAlphabet.ToLower().Contains(c);
    }
    
    public bool ValidateKey(int key)
    {
        if (key <= 0)
            return false;
            
        string keyStr = key.ToString();
        foreach (char digit in keyStr)
        {
            if (!char.IsDigit(digit))
                return false;
        }
        
        return true;
    }
}