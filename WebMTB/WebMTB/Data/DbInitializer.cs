using Microsoft.AspNetCore.Identity;

namespace WebMTB.Data
{
    public static class DbInitializer
    {
        public static async Task SeedData(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

            // 1. Tạo các Role nếu chưa tồn tại
            string[] roleNames = { "Admin", "User" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Lấy thông tin Admin từ appsettings.json
            var adminEmail = configuration["AdminAccount:Email"];
            var adminPassword = configuration["AdminAccount:Password"];

            // 3. Tạo tài khoản Admin mồi
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                var user = new IdentityUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true // Xác thực luôn để không cần gửi mail
                };

                var createPowerUser = await userManager.CreateAsync(user, adminPassword);
                if (createPowerUser.Succeeded)
                {
                    // Gán quyền Admin cho tài khoản này
                    await userManager.AddToRoleAsync(user, "Admin");
                }
            }
        }
    }
}