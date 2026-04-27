using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Защитная проверка: фиксированный хэш «password» из scripts/seed-demo-life.sql
    /// должен валидироваться текущей реализацией <see cref="Pbkdf2PasswordHasher"/>.
    /// Если кто-то поменяет формат / итерации / алгоритм — этот тест упадёт первым,
    /// и seed нужно будет пересоздать.
    /// </summary>
    public class SeedHashVerifyTests
    {
        private const string SeedHashForPassword =
            "100000.AQIDBAUGBwgJCgsMDQ4PEA==.7cyBZDaG9OlWsUaYsNJGCHei/cERxR/FFPfRr1R4A9M=";

        [Fact]
        public void Seed_demo_password_hash_verifies_against_password()
        {
            var hasher = new Pbkdf2PasswordHasher();
            Assert.True(hasher.Verify("password", SeedHashForPassword));
        }
    }
}
