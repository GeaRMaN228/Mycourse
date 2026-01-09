using System.Net.Http.Headers;
using System.Net.Http.Json;

internal class Program
{

    private static void Main(string[] args)
    {
        HttpClient client = new HttpClient();

        client.BaseAddress = new Uri("http://localhost:5000/");
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        
        string token = null;

        Console.OutputEncoding = System.Text.Encoding.GetEncoding("utf-16");
        Console.InputEncoding = System.Text.Encoding.GetEncoding("utf-16");
        Console.WriteLine("Здравствуйте, вы вошли в приложение Шифр Гронсфельда.");

        while (true)
        {
            if(token == null)
            {
                try
                {
                    token = Requests.SwitchLoginOrRegister(client);
                }catch(Exception){}
            }else
            {
                try
                {
                    token = Requests.SwitchActions(client, token);
                }catch(Exception){}
            }
        }        
    }
}

public static class Requests
{
    private static Answer_Request req = new Answer_Request();

    private static string Login(HttpClient client, int count = 0)
    {
        if(count == 5)
        {
            Console.WriteLine("Вы превысили максимальное количество попыток входа.\nНажмите любую клавишу для завершения работы приложения");
            Console.ReadKey();
            Environment.Exit(0);
        }
        Console.Write("Введите логин: ");
        string login = Console.ReadLine();
        Console.Write("Введите пароль: ");
        string password = Console.ReadLine();
        if(string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("Вы не ввели логин либо пароль!!");
            return Login(client, count);
        }
        var resp = req.MakeJson(login: login, password: password);
        HttpResponseMessage response = client.PostAsJsonAsync("/login", resp).Result;

        if (response.IsSuccessStatusCode)
        {
            var answer = response.Content.ReadFromJsonAsync<Answer_Request>().Result;
            Console.WriteLine(answer.Status);
            return answer.Token;
        }else return Login(client, count++);
    }

    private static void Register(HttpClient client)
    {
        Console.Write("Введите логин: ");
        string login = Console.ReadLine();
        Console.Write("Введите пароль: ");
        string password = Console.ReadLine();
        if(string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("Вы не ввели логин либо пароль!!");
            Register(client);
        }
        else
        {
            var resp = req.MakeJson(login: login, password: password);
            HttpResponseMessage response = client.PostAsJsonAsync("/register", resp).Result;
            if (response.IsSuccessStatusCode)
            {
                var answer = response.Content.ReadFromJsonAsync<Answer_Request>().Result;
                Console.WriteLine(answer.Status);
            }
        }
    }

    private static string Encrypt(HttpClient client, string token)
    {
        Console.Write("Введите текст: ");
        string text = Console.ReadLine();
        Console.Write("Введите ключ: ");
        int key = Convert.ToInt32(Console.ReadLine());
        if(string.IsNullOrEmpty(text) || key<0)
        {
            Console.WriteLine("Вы не ввели текст / ключ не верный!!");
            return Encrypt(client, token);
        }
        else
        {
            var resp = req.MakeJson(text: text, key: key, token: token);
            HttpResponseMessage response = client.PostAsJsonAsync("/encrypt", resp).Result;
            if (response.IsSuccessStatusCode)
            {
                var answer = response.Content.ReadFromJsonAsync<Answer_Request>().Result;
                Console.WriteLine(answer.Status);
                Console.WriteLine("Зашифрованный текст:\n"+answer.Text);
                return token;
            }else return null;
        }
    }

    private static string Decrypt(HttpClient client, string token)
    {
        Console.Write("Введите текст: ");
        string text = Console.ReadLine();
        Console.Write("Введите ключ: ");
        int key = Convert.ToInt32(Console.ReadLine());
        if(string.IsNullOrEmpty(text) || key<0)
        {
            Console.WriteLine("Вы не ввели текст / ключ не верный!!");
            return Decrypt(client, token);
        }
        else
        {
            var resp = req.MakeJson(text: text, key: key, token: token);
            HttpResponseMessage response = client.PostAsJsonAsync("/decrypt", resp).Result;
            if (response.IsSuccessStatusCode)
            {
                var answer = response.Content.ReadFromJsonAsync<Answer_Request>().Result;
                Console.WriteLine(answer.Status);
                Console.WriteLine("Расифрованный текст:\n"+answer.Text);
                return token;
            }else return null;
        }
    }
    private static string GetKey(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);     
        HttpResponseMessage response = client.GetAsync("/get/random_key").Result;
        if (response.IsSuccessStatusCode)
        {
            var answer = response.Content.ReadFromJsonAsync<Answer_Request>().Result;
            Console.WriteLine(answer.Status);
            Console.WriteLine("Ваш ключ: "+answer.Key);
            return token;
        }else return null;
    }

    private static string UpdatePassword(HttpClient client, string token)
    {
        Console.Write("Введите новый пароль: ");
        string new_pass = Console.ReadLine();
        if (string.IsNullOrEmpty(new_pass))
        {
            Console.WriteLine("Пароль не может быть пустым как ваша голова☻♥");
            return UpdatePassword(client, token);
        }
        else
        {
            var resp = req.MakeJson(token: token, password: new_pass);
            HttpResponseMessage response = client.PatchAsJsonAsync("/update/password", resp).Result;
            if (response.IsSuccessStatusCode)
            {
                var answer = response.Content.ReadFromJsonAsync<Answer_Request>().Result;
                Console.WriteLine(answer.Status);
                return answer.Token;
            }else return null;
        }
    }

    private static string AddText(HttpClient client, string token)//post
    {
        Console.Write("Введите текст: ");
        string text = Console.ReadLine();
        if (string.IsNullOrEmpty(text))
        {
            Console.WriteLine("Может ты хоть что-то напишешь? Пустая ты головешка ♥");
            return AddText(client, token);
        }
        else
        {
            var resp = req.MakeJson(token: token, text: text);
            HttpResponseMessage response = client.PostAsJsonAsync("/add/text", resp).Result;
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Текст сохранён.");
                return token;
            }else return null;
        }
    }
    
    private static string UpdateText(HttpClient client, string token)//patch
    {
        Console.Write("Введите id текста который вы хотите изменить: ");
        int text_id = Convert.ToInt32(Console.ReadLine());
        Console.WriteLine("Теперь введите текст(он полностью заменит текст сохранённый в БД(т.е. он УМРЁТ, по твоей вине ☻)): ");
        string text = Console.ReadLine();
        if (string.IsNullOrEmpty(text)|| !int.IsPositive(text_id))
        {
            Console.WriteLine("Ну тут либо ты дурак, либо у тебя клавиатура сломалась(id должно быть положительным, ну а текст должен быть)");
            return UpdateText(client, token);
        }
        else
        {
            var resp = req.MakeJson(token: token, text: text);
            HttpResponseMessage response = client.PatchAsJsonAsync($"/update/text/{text_id}", resp).Result;
            if (response.IsSuccessStatusCode)
            {
                var answer = response.Content.ReadFromJsonAsync<Answer_Request>().Result;
                Console.WriteLine(answer.Status);
                return token;
            }else return null;
        }
    }
    
    private static string GetText(HttpClient client, string token)//get
    {
        Console.Write("Введите id текста который вы хотите получить: ");
        int text_id = Convert.ToInt32(Console.ReadLine());
        if (!int.IsPositive(text_id))
        {
            Console.WriteLine("ID должен быть положительный");
            return GetText(client, token);
        }
        else
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);     
            HttpResponseMessage response = client.GetAsync($"/get/text/{text_id}").Result;
            if (response.IsSuccessStatusCode)
            {
                var answer = response.Content.ReadFromJsonAsync<Answer_Request>().Result;
                if(answer.Text == null)
                {
                    Console.WriteLine(answer.Status);
                }
                else
                {
                    Console.WriteLine(answer.Status);
                    Console.WriteLine("Ваш текст: "+answer.Text);
                }
                return token;
            }else return null;
        }
    }
    
    private static string GetTexts(HttpClient client, string token)//get
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);     
        HttpResponseMessage response = client.GetAsync($"/get/texts").Result;
        if (response.IsSuccessStatusCode)
        {
            Dictionary<int, string> answer = response.Content.ReadFromJsonAsync<Dictionary<int, string>>().Result;
            if(answer == null)
            {
                Console.WriteLine("У вас ещё нет текстов.");
            }
            else
            {
                foreach(KeyValuePair<int, string> pair in answer)
                    Console.WriteLine($"{pair.Key}: {pair.Value}");
            }
            return token;
        }else return null;
    }
    
    private static string DeleteText(HttpClient client, string token)//delete
    {
        Console.Write("Введите id текста который вы хотите удалить: ");
        int text_id = Convert.ToInt32(Console.ReadLine());
        if (!int.IsPositive(text_id))
        {
            Console.WriteLine("ID должен быть положительный");
            return GetText(client, token);
        }
        else
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            HttpResponseMessage response = client.DeleteAsync($"/delete/text/{text_id}").Result;
            if (response.IsSuccessStatusCode)
            {
                var answer = response.Content.ReadFromJsonAsync<Answer_Request>().Result;
                Console.WriteLine(answer.Status);
                return token;
            }else return null;
        }
    }
    
    private static string EncryptText(HttpClient client, string token)//post
    {
        Console.Write("Введите id текста который вы хотите зашифровать: ");
        int text_id = Convert.ToInt32(Console.ReadLine());
        Console.Write("Введите ключ для шифрования");
        int key = Convert.ToInt32(Console.ReadLine());
        if (!int.IsPositive(text_id) || !int.IsPositive(key))
        {
            Console.WriteLine("ID и ключ должны быть положительными");
            return EncryptText(client, token);
        }
        else
        {
            var resp = req.MakeJson(token: token, key: key);
            HttpResponseMessage response = client.PostAsJsonAsync($"/encrypt/text/{text_id}", resp).Result;
            if (response.IsSuccessStatusCode)
            {
                var answer = response.Content.ReadFromJsonAsync<Answer_Request>().Result;
                if(answer.Text == null)
                {
                    Console.WriteLine(answer.Status);
                }
                else
                {
                    Console.WriteLine(answer.Status);
                    Console.WriteLine("Ваш шифр-текст: "+answer.Text);
                }
                return token;
            }else return null;
        }
    }
    
    private static string DecryptText(HttpClient client, string token)//post
    {
        Console.Write("Введите id текста который вы хотите зашифровать: ");
        int text_id = Convert.ToInt32(Console.ReadLine());
        Console.Write("Введите ключ для дешифрования");
        int key = Convert.ToInt32(Console.ReadLine());
        if (!int.IsPositive(text_id) || !int.IsPositive(key))
        {
            Console.WriteLine("ID и ключ должны быть положительными");
            return DecryptText(client, token);
        }
        else
        {
            var resp = req.MakeJson(token: token, key: key);
            HttpResponseMessage response = client.PostAsJsonAsync($"/decrypt/text/{text_id}", resp).Result;
            if (response.IsSuccessStatusCode)
            {
                var answer = response.Content.ReadFromJsonAsync<Answer_Request>().Result;
                if(answer.Text == null)
                {
                    Console.WriteLine(answer.Status);
                }
                else
                {
                    Console.WriteLine(answer.Status);
                    Console.WriteLine("Ваш расшифрованный текст: "+answer.Text);
                }
                return token;
            }else return null;
        }
    }
    
    private static string GetLogs(HttpClient client, string token)//get
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);     
        HttpResponseMessage response = client.GetAsync($"/get/logs/").Result;
        if (response.IsSuccessStatusCode)
        {
            Dictionary<int, string> answer = response.Content.ReadFromJsonAsync<Dictionary<int, string>>().Result;

            foreach(KeyValuePair<int, string> pair in answer)
                Console.WriteLine($"{pair.Key}: {pair.Value}");
            return token;
        }else return null;
    }
    
    private static string LogOut(HttpClient client, string token)
    {
        var resp = req.MakeJson(token: token);
        HttpResponseMessage response = client.PostAsJsonAsync($"/logout", resp).Result;
        if(response.IsSuccessStatusCode)
            Console.WriteLine("Выход из аккаунта...");
        return null;
    }
    
    public static string SwitchActions(HttpClient client, string token)
    {
        Console.WriteLine(@"Выберите действие
1) Добавить текст
2) Изменить текст
3) Вывести конкретный текст
4) Вывести все тексты
5) Удалить текст
6) Зашифровать текст
7) Зашифровать текст в БД
8) Расшифровать текст
9) Расшифровать текст в бд
10) Сгенерировать случайный ключ
11) Сменить пароль
12) Вывести логи
0) Выйти из аккаунта");
        int choise = Convert.ToInt32(Console.ReadLine());
        switch (choise)
        {
            case 1:
                return AddText(client, token);
            case 2:
                return UpdateText(client, token);
            case 3:
                return GetText(client, token);
            case 4:
                return GetTexts(client, token);
            case 5:
                return DeleteText(client, token);
            case 6:
                return Encrypt(client, token);
            case 7:
                return EncryptText(client, token);
            case 8:
                return Decrypt(client, token);
            case 9:
                return DecryptText(client, token);
            case 10:
                return GetKey(client, token);
            case 11:
                return UpdatePassword(client, token);
            case 12:
                return GetLogs(client, token);
            case 0:
                return LogOut(client, token);
        }
        return token;
    }

    public static string SwitchLoginOrRegister(HttpClient client)
    {
        Console.WriteLine("Выберите действие\n1) Зарегистрироваться\n2) Войти\n0) Выйти из приложения");
        int choise = Convert.ToInt32(Console.ReadLine());
        switch (choise)
        {
            case 1:
                Console.WriteLine("Регистрация");
                Register(client);
                return null;
            case 2:
                Console.WriteLine("Вход");
                string token = Login(client);
                return token;
            case 0:
                Environment.Exit(0);
                break;
        }
        return null;
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
