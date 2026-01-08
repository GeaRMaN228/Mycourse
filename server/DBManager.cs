using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

public class DBManager {
    private SqliteConnection? connection = null;

    private string HashPassword(string password) {
        using (var algorithm = SHA256.Create()) {
            var bytes_hash = algorithm.ComputeHash(Encoding.Unicode.GetBytes(password));
            return Encoding.Unicode.GetString(bytes_hash);
        }
    }
    
    public bool ConnectToDB(string path) {
        Console.WriteLine("Connection to db...");

        try
        {
            connection = new SqliteConnection("Data Source=" + path);
            connection.Open();

            if (connection.State != ConnectionState.Open) {
                Console.WriteLine("Failed!");
                return false;
            }
        }
        catch (Exception exp) {
            Console.WriteLine(exp.Message);
            return false;
        }

        Console.WriteLine("Done!");
        return true;
    }

    public void Disconnect() {
        if (null == connection)
            throw new Exception("Соединение с БД не установлено");

        if (connection.State != ConnectionState.Open)
            throw new Exception("Соединение с БД не открыто");

        connection.Close();

        Console.WriteLine("Disconnect from db");
    }

    public void CreateTables()
    {
        if (null == connection)
            throw new Exception("Соединение с БД не установлено");

        if (connection.State != ConnectionState.Open)
            throw new Exception("Соединение с БД не открыто");

        string createUsersTable = "CREATE TABLE IF NOT EXISTS users (id INTEGER PRIMARY KEY, user TEXT UNIQUE NOT NULL, password TEXT NOT NULL)";

        string createTokensTable = "CREATE TABLE IF NOT EXISTS tokens (user_id INTEGER NOT NULL, token TEXT UNIQUE NOT NULL, create_date DATETIME DEFAULT CURRENT_TIMESTAMP, end_date TEXT NOT NULL)";

        string createLogsTable = "CREATE TABLE IF NOT EXISTS log (user_id INTEGER NOT NULL, user_action TEXT NOT NULL, data_action DATETIME DEFAULT CURRENT_TIMESTAMP)";

        string createTextsTable = @"CREATE TABLE IF NOT EXISTS texts (user_id INTEGER NOT NULL, text_id INTEGER NOT NULL , text TEXT NOT NULL, PRIMARY KEY (user_id, text_id));
        CREATE TRIGGER IF NOT EXISTS autoincrement_text_id
        BEFORE INSERT ON texts
        BEGIN
            SELECT CASE 
                WHEN (SELECT COUNT(*) FROM texts WHERE user_id = NEW.user_id) = 0 
                THEN 1
                ELSE (SELECT MAX(text_id) + 1 FROM texts WHERE user_id = NEW.user_id)
            END;
            
            INSERT OR REPLACE INTO texts (user_id, text_id, text)
            VALUES (
                NEW.user_id,
                COALESCE(
                    (SELECT MAX(text_id) + 1 FROM texts WHERE user_id = NEW.user_id),
                    1
                ),
                NEW.text
            );
            
            SELECT RAISE(IGNORE);
        END;";

        try
        {
            var command1 = new SqliteCommand(createUsersTable, connection);
            var command2 = new SqliteCommand(createLogsTable, connection);
            var command3 = new SqliteCommand(createTokensTable, connection);
            var command4 = new SqliteCommand(createTextsTable, connection);
            object result1 = command1.ExecuteNonQuery();
            object result2 = command2.ExecuteNonQuery();
            object result3 = command3.ExecuteNonQuery();
            object result4 = command4.ExecuteNonQuery();
        }
        catch (Exception exp)
        {
            Console.WriteLine(exp);
        }
    }

    public int GetUserId(string login = null, string token = null)
    {
        if (string.IsNullOrEmpty(token))
        {
            string REQUEST = "SELECT id FROM users WHERE user=\""+login+"\"";
            var command = new SqliteCommand(REQUEST,connection);
            object result = command.ExecuteScalar();
            return Convert.ToInt32(result);
        }
        if (string.IsNullOrEmpty(login))
        {
            string REQUEST = "SELECT user_id FROM tokens WHERE token=\""+token+"\"";
            var command = new SqliteCommand(REQUEST,connection);
            object result = command.ExecuteScalar();
            return Convert.ToInt32(result);
        }else return -1;
    }

    public void LogoutLog(string token)
    {
        int us_id = GetUserId(token: token);
        string REQUEST_log = "INSERT INTO log (user_id, user_action) VALUES ("+us_id+", "+"\"Выход из аккаунта\""+")";
        var command = new SqliteCommand(REQUEST_log, connection);
        command.ExecuteNonQuery();
    }

    public void AddLogEncrypt(string token)
    {
        int us_id = GetUserId(token: token);
        string REQUEST_log = "INSERT INTO log (user_id, user_action) VALUES ("+us_id+", "+"\"Текст зашифрован\""+")";
        var command = new SqliteCommand(REQUEST_log, connection);
        command.ExecuteNonQuery();
    }
    public void AddLogEncryptText(string token)
    {
        int us_id = GetUserId(token: token);
        string REQUEST_log = "INSERT INTO log (user_id, user_action) VALUES ("+us_id+", "+"\"Текст зашифрован и сохранён в БД\""+")";
        var command = new SqliteCommand(REQUEST_log, connection);
        command.ExecuteNonQuery();
    }
    public void AddLogDecrypt(string token)
    {
        int us_id = GetUserId(token: token);
        string REQUEST_log = "INSERT INTO log (user_id, user_action) VALUES ("+us_id+", "+"\"Текст расшифрован\""+")";
        var command = new SqliteCommand(REQUEST_log, connection);
        command.ExecuteNonQuery();
    }

    public void AddLogDecryptText(string token)
    {
        int us_id = GetUserId(token: token);
        string REQUEST_log = "INSERT INTO log (user_id, user_action) VALUES ("+us_id+", "+"\"Текст расшифрован и сохранён в БД\""+")";
        var command = new SqliteCommand(REQUEST_log, connection);
        command.ExecuteNonQuery();
    }

    public void AddText(string token, string text)
    {
        int us_id = GetUserId(token: token);
        string REQUEST = "INSERT INTO texts (user_id, text) VALUES ("+us_id+",\""+text+"\")";
        string REQUEST_log = "INSERT INTO log (user_id, user_action) VALUES ("+us_id+",\"Текст добавлен\")";
        var command = new SqliteCommand(REQUEST, connection);
        var command_log = new SqliteCommand(REQUEST_log, connection);
        command.ExecuteNonQuery();
        command_log.ExecuteNonQuery();
    }

    public string GetText(string token, int text_id)
    {
        int us_id = GetUserId(token: token);
        string REQUEST = "SELECT text FROM texts WHERE user_id="+us_id+" AND text_id="+text_id;
        var command = new SqliteCommand(REQUEST, connection);
        string REQUEST_log = "INSERT INTO log (user_id, user_action) VALUES ("+us_id+",\"Текст запрошен\")";
        var command_log = new SqliteCommand(REQUEST_log, connection);
        var reader = command.ExecuteReader();
        if (reader.HasRows)
        {
            command_log.ExecuteNonQuery();
            reader.Read();
            return reader.GetString(0);
        }else return null;
        
    }

    public Dictionary<int, string> GetTexts(string token)
    {
        var texts = new Dictionary<int, string>();
        int us_id = GetUserId(token: token);
        string REQUEST = "SELECT text_id, text FROM texts WHERE user_id="+us_id;
        var command = new SqliteCommand(REQUEST, connection);
        var reader = command.ExecuteReader();
        string REQUEST_log = "INSERT INTO log (user_id, user_action) VALUES ("+us_id+",\"Тексты запрошены\")";
        var command_log = new SqliteCommand(REQUEST_log, connection);
        while (reader.Read())
        {
            texts.Add(reader.GetInt32(0), reader.GetString(1));
        }
        command_log.ExecuteNonQuery();
        return texts;
    }

    public void DeleteText(string token, int text_id)
    {
        int us_id = GetUserId(token: token);
        string REQUEST = "DELETE FROM texts WHERE user_id="+us_id+" AND text_id="+text_id;
        var command = new SqliteCommand(REQUEST, connection);
        command.ExecuteNonQuery();
    }

    public bool UpdateText(string token, int text_id, string text)
    {
        int us_id = GetUserId(token: token);
        string REQUEST = "UPDATE texts SET text=\""+text+"\" WHERE user_id="+us_id+" AND text_id="+text_id;
        var command = new SqliteCommand(REQUEST, connection);
        string REQUEST_log = "INSERT INTO log (user_id, user_action) VALUES ("+us_id+",\"Текст изменён\")";
        var command_log = new SqliteCommand(REQUEST_log, connection);
        if (command.ExecuteNonQuery() == 0)
        {
            return false;
        }
        else 
        {
            command_log.ExecuteNonQuery();
            return true;
        }

    }

    public void AddLogKeyGen(string Token)
    {
        int us_id = GetUserId(token: Token);
        string REQUEST_log = "INSERT INTO log (user_id, user_action) VALUES ("+us_id+", "+"\"Ключ сгенерирован\""+")";
        var command = new SqliteCommand(REQUEST_log, connection);
        command.ExecuteNonQuery();
    }

    public Dictionary<int, string> GetLogs(string Token)
    {
        int us_id = GetUserId(token: Token);
        string REQUEST = "SELECT data_action, user_action FROM log WHERE user_id ="+us_id;
        Dictionary<int, string> logs = new Dictionary<int, string>();
        var command = new SqliteCommand(REQUEST, connection);
        var reader = command.ExecuteReader();
        int count = 1;
        while (reader.Read())
        {
            logs.Add(count ,Convert.ToDateTime(reader.GetDateTime(0)) +"   "+ reader.GetString(1));
            count++;
        }
        return logs;
    }

    public bool ValidateToken(string token)
    {
        string REQUEST = "SELECT end_date FROM tokens WHERE token=\""+token+"\"";
        var command = new SqliteCommand(REQUEST, connection);
        var reader = command.ExecuteReader();
        try
        {
            reader.Read();
            DateTime end_date = Convert.ToDateTime(reader.GetString(reader.GetOrdinal("end_date")));
            if(DateTime.UtcNow<end_date)
                return true;
            else return false;
        }
        catch(Exception exp)
        {
            Console.WriteLine(exp);
            return false;
        }
    }

    public bool UpdatePassword(string token, string new_pass)
    {
        int user_id = GetUserId(token: token);
        string REQUEST = "UPDATE users SET password=\""+HashPassword(new_pass)+"\" WHERE id=\""+user_id+"\"";
        var command = new SqliteCommand(REQUEST, connection);
        string REQUEST_log = "INSERT INTO log (user_id, user_action) VALUES ("+user_id+",\"Пароль изменён\")";
        var command_log = new SqliteCommand(REQUEST_log, connection);
        try
        {
            command.ExecuteNonQuery();
            command_log.ExecuteNonQuery();
            return true;
        }catch(Exception exp)
        {
            Console.WriteLine(exp);
            return false;
        }        
    }

    public bool UserExist(string login)
    {
        string REQUEST = "SELECT 1 FROM users WHERE user=\""+login+"\"";
        var command = new SqliteCommand(REQUEST,connection);
        int result = Convert.ToInt32(command.ExecuteScalar());
        if (result == 1) return true;
        return false;
    }

    public bool AddUser(string login, string password) 
    {
        if (null == connection)
            throw new Exception("Соединение с БД не установлено");

        if (connection.State != ConnectionState.Open)
            throw new Exception("Соединение с БД не открыто");

        if (UserExist(login)) throw new Exception("Данный пользователь уже существует.");

        if (string.IsNullOrEmpty(login)) throw new Exception("Логин не должен быть пустым");
        if (string.IsNullOrEmpty(password)) throw new Exception("Пароль не должен быть пустым");

        string REQUEST = "INSERT INTO users (user, password) VALUES (\"" + login + "\", \"" + HashPassword(password) + "\")";

        var command = new SqliteCommand(REQUEST, connection);
        
        int result = 0;
        try
        {
            result = command.ExecuteNonQuery();
        }
        catch (Exception exp) {
            Console.WriteLine(exp.Message);
            return false;
        }
        int us_id = GetUserId(login);
        string REQUEST_log = "INSERT INTO log (user_id, user_action) VALUES ("+us_id+", "+"\"Пользователь зарегистрирован\""+")";
        var command2 = new SqliteCommand(REQUEST_log, connection);
        command2.ExecuteNonQuery();
        if (1 == result)
            return true;
        else
            return false;
    }
    
    public void DeleteOtherTokens(int user_id)
    {
        string REQUEST = "DELETE FROM tokens WHERE user_id="+user_id;
        var command = new SqliteCommand(REQUEST, connection);
        command.ExecuteNonQuery();
    }

    public void AddToken(string token, string login = null)
    {
        int user_id;
        if (login ==null)
        {
            user_id = GetUserId(token: token);
        }else user_id = GetUserId(login: login);
        DeleteOtherTokens(user_id);
        string REQUEST = "INSERT INTO tokens (user_id, token, end_date) VALUES (\""+user_id+"\", \""+token+"\", \""+DateTime.UtcNow.AddMinutes(30)+"\")";
        var command = new SqliteCommand(REQUEST, connection);
        command.ExecuteNonQuery();
        
    }
    
    public bool Login(string login, string password) {
        if (null == connection)
            throw new Exception("Соединение с БД не установлено");

        if (connection.State != ConnectionState.Open)
            throw new Exception("Соединение с БД не открыто");
        
        string REQUEST = "SELECT user,Password FROM users WHERE user=\"" + login + "\" AND password = \"" + HashPassword(password) + "\"";
        var command = new SqliteCommand(REQUEST, connection);
        int user_id = GetUserId(login);
        string REQUEST_log = "INSERT INTO log (user_id, user_action) VALUES (\""+user_id+"\", \"Выполнен вход\")";
        var command2 = new SqliteCommand(REQUEST_log, connection);
        try
        {
            var reader = command.ExecuteReader();
            
            if (reader.HasRows)
            {
                command2.ExecuteNonQuery();
                return true;
            }else
            {
                string REQUEST_bad_try = "INSERT INTO log (user_id, user_action) VALUES (\""+user_id+"\", \"Неудачная попытка входа\")";
                var command_bad_try = new SqliteCommand(REQUEST_bad_try, connection);
                command_bad_try.ExecuteNonQuery(); 
                throw new Exception("Пароль не совпадает, или такого пользователя не существует");
            }
        }
        catch (Exception exp) {
            Console.WriteLine(exp.Message);
            return false;
        }
    }

}
