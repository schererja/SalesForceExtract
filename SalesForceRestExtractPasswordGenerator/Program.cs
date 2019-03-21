using System;

namespace SalesForceRestExtractPasswordGenerator
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Sales Force Rest Extract Password Generator");
            Console.WriteLine("Please enter password of Sales Force User.");
            Console.WriteLine("Enter Password:");
            var password = Console.ReadLine();
            Console.WriteLine(
                "WARNING: This token needs to be exactly the same as one in the data extract application.");
            Console.WriteLine("Enter Sales Force Security Token:");
            var securityToken = Console.ReadLine();
            var cipher = new Cipher();
            Console.Clear();
            var encryptedPassword = cipher.Encrypt(password, securityToken);
            Console.WriteLine("Entered Password: " + password);
            Console.WriteLine("Encrypted Password: " + encryptedPassword);
            Console.WriteLine("Security Token Entered: " + securityToken);
            Console.Read();
        }
    }
}