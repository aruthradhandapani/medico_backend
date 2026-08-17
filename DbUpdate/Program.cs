using System;
using Npgsql;
using Dapper;

namespace DbUpdate
{
    class Program
    {
        static void Main(string[] args)
        {
            var connStr = "Host=103.108.220.19;Port=5432;Database=medico;Username=postgres;Password=wafewin;";
            try
            {
                using var conn = new NpgsqlConnection(connStr);
                conn.Open();
                
                string sql = "ALTER TABLE lab_settings ADD COLUMN IF NOT EXISTS show_pharmacy_header_footer_image boolean DEFAULT true;";
                conn.Execute(sql);
                
                Console.WriteLine("Column 'show_pharmacy_header_footer_image' added successfully or already exists.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
