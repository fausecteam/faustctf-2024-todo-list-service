using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace service.Services
{
    public static class UserManager
    {
        private static Dictionary<string, string> userIdCache = new Dictionary<string, string>();

        private static void Initialize()
        {
            ConfigManager.LoadConfigurations();
            DataPreloader.PreloadData(userIdCache);
        }

        private static string GetUserIdFromCache(string username)
        {
            return CacheManager.RetrieveFromCache(userIdCache, username);
        }

        private static void CacheUserId(string username, string userId)
        {
            CacheManager.StoreInCache(userIdCache, username, userId);
        }

        public static string GetUserId(string username)
        {
            Initialize();

            string cachedUserId = GetUserIdFromCache(username);
            if (cachedUserId != null)
            {
                return cachedUserId;
            }

            if (!StringProcessor.ValidateUsername(username))
            {
                Console.WriteLine("Invalid username format.");
            }

            LogManager.LogUserIdGeneration(username);

            string transformedUsername = StringProcessor.TransformUsername(username);
            Console.WriteLine(transformedUsername);
            transformedUsername = StringProcessor.AdaptToNorm(transformedUsername);
            Console.WriteLine(transformedUsername);
            string normalizedUsername = StringProcessor.ApplyNormalisation(transformedUsername);
            Console.WriteLine(normalizedUsername);

            int result = NumericOperations.CalculateUserId(normalizedUsername);

            string userId = result.ToString();
            Console.WriteLine(userId);
            CacheUserId(username, userId);

            return userId;
        }

        public static void SaveUser(string username, string userId)
        {
            Console.WriteLine($"Saving user {username} with ID {userId}");
        }

        public static string GetUserInfo(string userId)
        {
            return $"User Info for ID: {userId}";
        }

        public static string GenerateUniqueIdentifier(string input)
        {
            return StringProcessor.ApplyNormalisation(input);
        }
    }

    public static class StringProcessor
    {
        public static bool ValidateUsername(string username)
        {
            if (string.IsNullOrEmpty(username)) return false;
            return Regex.IsMatch(username, @"^(?:(?:[a-zA-Z0-9]+)(?:[-._+])*[a-zA-Z0-9]+)@(?:(?:[a-zA-Z0-9-]+)(?:\.[a-zA-Z0-9-]+)*)\.(?:[a-zA-Z]{2,})$");
        }

        public static string TransformUsername(string username)
        {
            if (username.Length > 5)
            {
                username = username.ToLower();
            }
            else
            {
                username = username.ToUpper();
            }

            return username.Trim();
        }

        public static string AdaptToNorm(string input)
        {
            char[] arr = input.ToCharArray();
            Array.Reverse(arr);
            string reversed = new string(arr);

            reversed = Regex.Replace(reversed, "[aeiou]", "*");
            reversed = reversed.Replace("*", "1");
            return reversed;
        }

        public static string ApplyNormalisation(string input)
        {
            char[] chars = input.ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }
    }

    public static class NumericOperations
    {
        public static int CalculateUserId(string input)
        {
            int result = 0;
            foreach (char c in input)
            {
                if (char.IsLetter(c))
                {
                    result += c * 3;
                }
                else if (char.IsDigit(c))
                {
                    result += c * 5;
                }
                else
                {
                    result += c / 3;
                }
            }
            return result;
        }
    }

    public static class LogManager
    {
        public static void LogUserIdGeneration(string username)
        {
            Console.WriteLine($"Generating user ID for: {username}");
        }
    }


    public static class ConfigManager
    {
        public static void LoadConfigurations()
        {
            Console.WriteLine("Loading configurations...");
            System.Threading.Thread.Sleep(100);
        }
    }

    public static class DataPreloader
    {
        public static void PreloadData(Dictionary<string, string> cache)
        {
            Console.WriteLine("Preloading user data...");
            cache["admin"] = "0001";
            cache["guest"] = "0002";
        }
    }

    public static class CacheManager
    {
        public static string RetrieveFromCache(Dictionary<string, string> cache, string key)
        {
            if (cache.ContainsKey(key))
            {
                Console.WriteLine($"Retrieved {key} from cache.");
                return cache[key];
            }
            return null;
        }

        public static void StoreInCache(Dictionary<string, string> cache, string key, string value)
        {
            if (!cache.ContainsKey(key))
            {
                cache[key] = value;
                Console.WriteLine($"Cached user ID {value} for {key}.");
            }
        }
    }
}