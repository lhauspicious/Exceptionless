﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Api.Models;
using Exceptionless.Api.Tests.Authentication;
using Exceptionless.Api.Tests.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Utility;
using Foundatio.Repositories;
using Xunit;
using Xunit.Abstractions;
using User = Exceptionless.Core.Models.User;

namespace Exceptionless.Api.Tests.Controllers {
    public class AuthControllerTests : IntegrationTestsBase {
        private readonly IUserRepository _userRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;

        public AuthControllerTests(ITestOutputHelper output) : base(output) {
            Settings.Current.EnableAccountCreation = true;
            Settings.Current.EnableActiveDirectoryAuth = false;

            _organizationRepository = GetService<IOrganizationRepository>();
            _projectRepository = GetService<IProjectRepository>();
            _userRepository = GetService<IUserRepository>();
            CreateOrganizationAndProjectsAsync().GetAwaiter().GetResult();
        }

        [Theory]
        [InlineData(true, TestDomainLoginProvider.ValidUsername, TestDomainLoginProvider.ValidPassword)]
        [InlineData(true, "test1.2@exceptionless.io", TestDomainLoginProvider.ValidPassword)]
        [InlineData(false, "test1@exceptionless.io", "Password1$")]
        public async Task CannotSignupWhenAccountCreationDisabledWithNoTokenAsync(bool enableAdAuth, string email, string password) {
            Settings.Current.EnableAccountCreation = false;
            Settings.Current.EnableActiveDirectoryAuth = enableAdAuth;

            if (enableAdAuth && email == TestDomainLoginProvider.ValidUsername) {
                var provider = new TestDomainLoginProvider();
                email = provider.GetEmailAddressFromUsername(email);
            }

            await SendRequest(r => r
                .Post()
                .AppendPath("auth/signup")
                .Content(new SignupModel {
                    Email = email,
                    InviteToken = "",
                    Name = "Test",
                    Password = password
                })
                .StatusCodeShouldBeBadRequest()
            );
        }

        [Theory]
        [InlineData(true, TestDomainLoginProvider.ValidUsername, TestDomainLoginProvider.ValidPassword)]
        [InlineData(true, "test2.2@exceptionless.io", TestDomainLoginProvider.ValidPassword)]
        [InlineData(false, "test2@exceptionless.io", "Password1$")]
        public async Task CannotSignupWhenAccountCreationDisabledWithInvalidTokenAsync(bool enableAdAuth, string email, string password) {
            Settings.Current.EnableAccountCreation = false;
            Settings.Current.EnableActiveDirectoryAuth = enableAdAuth;

            if (enableAdAuth && email == TestDomainLoginProvider.ValidUsername) {
                var provider = new TestDomainLoginProvider();
                email = provider.GetEmailAddressFromUsername(email);
            }

            await SendRequest(r => r
                .Post()
                .AppendPath("auth/signup")
                .Content(new SignupModel {
                    Email = email,
                    InviteToken = StringExtensions.GetNewToken(),
                    Name = "Test",
                    Password = password
                })
                .StatusCodeShouldBeBadRequest()
            );
        }

        [Theory]
        [InlineData(true, TestDomainLoginProvider.ValidUsername, TestDomainLoginProvider.ValidPassword)]
        [InlineData(false, "test3@exceptionless.io", "Password1$")]
        public async Task CanSignupWhenAccountCreationDisabledWithValidTokenAsync(bool enableAdAuth, string email, string password) {
            Settings.Current.EnableAccountCreation = false;
            Settings.Current.EnableActiveDirectoryAuth = enableAdAuth;

            if (enableAdAuth && email == TestDomainLoginProvider.ValidUsername) {
                var provider = new TestDomainLoginProvider();
                email = provider.GetEmailAddressFromUsername(email);
            }

            var results = await _organizationRepository.GetAllAsync();
            var organization = results.Documents.First();

            var invite = new Invite {
                Token = StringExtensions.GetNewToken(),
                EmailAddress = email.ToLowerInvariant(),
                DateAdded = SystemClock.UtcNow
            };
            organization.Invites.Add(invite);
            organization = await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
            Assert.NotNull(organization.GetInvite(invite.Token));

            var result = await SendRequestAs<TokenResult>(r => r
               .Post()
               .AppendPath("auth/signup")
               .Content(new SignupModel {
                   Email = email,
                   InviteToken = invite.Token,
                   Name = "Test",
                   Password = password
               })
               .StatusCodeShouldBeOk()
           );

            Assert.NotNull(result);
            Assert.False(String.IsNullOrEmpty(result.Token));
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationDisabledWithValidTokenAndInvalidAdAccountAsync() {
            Settings.Current.EnableAccountCreation = false;
            Settings.Current.EnableActiveDirectoryAuth = true;

            string email = "testuser1@exceptionless.io";
            string password = "invalidAccount1";

            var orgs = await _organizationRepository.GetAllAsync();
            var organization = orgs.Documents.First();
            var invite = new Invite {
                Token = StringExtensions.GetNewToken(),
                EmailAddress = email.ToLowerInvariant(),
                DateAdded = SystemClock.UtcNow
            };

            organization.Invites.Add(invite);
            await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
            Assert.NotNull(organization.GetInvite(invite.Token));

            await SendRequest(r => r
               .Post()
               .AppendPath("auth/signup")
               .Content(new SignupModel {
                   Email = email,
                   InviteToken = invite.Token,
                   Name = "Test",
                   Password = password
               })
               .StatusCodeShouldBeBadRequest()
            );
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithNoTokenAsync() {
            Settings.Current.EnableAccountCreation = true;

            var result = await SendRequestAs<TokenResult>(r => r
               .Post()
               .AppendPath("auth/signup")
               .Content(new SignupModel {
                   Email = "test4@exceptionless.io",
                   InviteToken = "",
                   Name = "Test",
                   Password = "Password1$"
               })
               .StatusCodeShouldBeOk()
            );

            Assert.NotNull(result);
            Assert.False(String.IsNullOrEmpty(result.Token));
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithNoTokenAndValidAdAccountAsync() {
            Settings.Current.EnableAccountCreation = true;
            Settings.Current.EnableActiveDirectoryAuth = true;

            var provider = new TestDomainLoginProvider();
            string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);

            var result = await SendRequestAs<TokenResult>(r => r
               .Post()
               .AppendPath("auth/signup")
               .Content(new SignupModel {
                   Email = email,
                   InviteToken = "",
                   Name = "Test",
                   Password = TestDomainLoginProvider.ValidPassword
               })
               .StatusCodeShouldBeOk()
            );

            Assert.NotNull(result);
            Assert.False(String.IsNullOrEmpty(result.Token));
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithNoTokenAndInvalidAdAccountAsync() {
            Settings.Current.EnableAccountCreation = true;
            Settings.Current.EnableActiveDirectoryAuth = true;

            await SendRequest(r => r
               .Post()
               .AppendPath("auth/signup")
               .Content(new SignupModel {
                   Email = "testuser2@exceptionless.io",
                   InviteToken = "",
                   Name = "Test",
                   Password = "literallydoesntmatter"
               })
               .StatusCodeShouldBeBadRequest()
            );
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithValidTokenAsync() {
            Settings.Current.EnableAccountCreation = true;

            var orgs = await _organizationRepository.GetAllAsync();
            var organization = orgs.Documents.First();
            string email = "test5@exceptionless.io";
            var invite = new Invite {
                Token = StringExtensions.GetNewToken(),
                EmailAddress = email.ToLowerInvariant(),
                DateAdded = SystemClock.UtcNow
            };
            organization.Invites.Add(invite);
            await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
            Assert.NotNull(organization.GetInvite(invite.Token));

            var result = await SendRequestAs<TokenResult>(r => r
               .Post()
               .AppendPath("auth/signup")
               .Content(new SignupModel {
                   Email = email,
                   InviteToken = invite.Token,
                   Name = "Test",
                   Password = "Password1$"
               })
               .StatusCodeShouldBeOk()
            );

            Assert.NotNull(result);
            Assert.False(String.IsNullOrEmpty(result.Token));
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithValidTokenAndValidAdAccountAsync() {
            Settings.Current.EnableAccountCreation = true;
            Settings.Current.EnableActiveDirectoryAuth = true;

            var provider = new TestDomainLoginProvider();
            string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);

            var results = await _organizationRepository.GetAllAsync();
            var organization = results.Documents.First();
            var invite = new Invite {
                Token = StringExtensions.GetNewToken(),
                EmailAddress = email.ToLowerInvariant(),
                DateAdded = SystemClock.UtcNow
            };
            organization.Invites.Add(invite);
            await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
            Assert.NotNull(organization.GetInvite(invite.Token));

            var result = await SendRequestAs<TokenResult>(r => r
               .Post()
               .AppendPath("auth/signup")
               .Content(new SignupModel {
                   Email = email,
                   InviteToken = invite.Token,
                   Name = "Test",
                   Password = TestDomainLoginProvider.ValidPassword
               })
               .StatusCodeShouldBeOk()
            );

            Assert.NotNull(result);
            Assert.False(String.IsNullOrEmpty(result.Token));
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithValidTokenAndInvalidAdAccountAsync() {
            Settings.Current.EnableAccountCreation = true;
            Settings.Current.EnableActiveDirectoryAuth = true;

            string email = "testuser4@exceptionless.io";
            var results = await _organizationRepository.GetAllAsync();
            var organization = results.Documents.First();
            var invite = new Invite {
                Token = StringExtensions.GetNewToken(),
                EmailAddress = email.ToLowerInvariant(),
                DateAdded = SystemClock.UtcNow
            };
            organization.Invites.Add(invite);
            await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
            Assert.NotNull(organization.GetInvite(invite.Token));

            await SendRequest(r => r
               .Post()
               .AppendPath("auth/signup")
               .Content(new SignupModel {
                   Email = email,
                   InviteToken = invite.Token,
                   Name = "Test",
                   Password = TestDomainLoginProvider.ValidPassword
               })
               .StatusCodeShouldBeBadRequest()
            );
        }

        [Fact]
        public async Task LoginValidAsync() {
            Settings.Current.EnableActiveDirectoryAuth = false;

            const string email = "test6@exceptionless.io";
            const string password = "Test6 password";
            const string salt = "1234567890123456";
            string passwordHash = password.ToSaltedHash(salt);
            var user = new User {
                EmailAddress = email,
                Password = passwordHash,
                Salt = salt,
                IsEmailAddressVerified = true,
                FullName = "User 6"
            };
            await _userRepository.AddAsync(user, o => o.ImmediateConsistency());

            var result = await SendRequestAs<TokenResult>(r => r
               .Post()
               .AppendPath("auth/login")
               .Content(new LoginModel {
                   Email = email,
                   Password = password
               })
               .StatusCodeShouldBeOk()
            );

            Assert.NotNull(result);
            Assert.False(String.IsNullOrEmpty(result.Token));
        }

        [Fact]
        public async Task LoginInvalidPasswordAsync() {
            Settings.Current.EnableActiveDirectoryAuth = false;

            const string email = "test7@exceptionless.io";
            const string password = "Test7 password";
            const string salt = "1234567890123456";
            string passwordHash = password.ToSaltedHash(salt);

            var user = new User {
                EmailAddress = email,
                Password = passwordHash,
                Salt = salt,
                IsEmailAddressVerified = true,
                FullName = "User 7"
            };

            await _userRepository.AddAsync(user, o => o.ImmediateConsistency());

            await SendRequest(r => r
               .Post()
               .AppendPath("auth/login")
               .Content(new LoginModel {
                   Email = email,
                   Password = "This password ain't right"
               })
               .StatusCodeShouldBeUnauthorized()
            );
        }

        [Fact]
        public async Task LoginNoSuchUserAsync() {
            Settings.Current.EnableActiveDirectoryAuth = false;

            const string email = "test8@exceptionless.io";
            const string password = "Test8 password";
            const string salt = "1234567890123456";
            string passwordHash = password.ToSaltedHash(salt);
            var user = new User {
                EmailAddress = email,
                Password = passwordHash,
                Salt = salt,
                IsEmailAddressVerified = true,
                FullName = "User 8"
            };
            await _userRepository.AddAsync(user, o => o.ImmediateConsistency());

            await SendRequest(r => r
               .Post()
               .AppendPath("auth/login")
               .Content(new LoginModel {
                   Email = "Thisguydoesntexist@exceptionless.io",
                   Password = "This password ain't right"
               })
               .StatusCodeShouldBeUnauthorized()
            );
        }

        [Fact]
        public async Task LoginValidExistingActiveDirectoryAsync() {
            Settings.Current.EnableActiveDirectoryAuth = true;

            var provider = new TestDomainLoginProvider();
            string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);
            var user = new User {
                EmailAddress = email,
                IsEmailAddressVerified = true,
                FullName = "User 6"
            };

            await _userRepository.AddAsync(user, o => o.ImmediateConsistency());

            var result = await SendRequestAs<TokenResult>(r => r
               .Post()
               .AppendPath("auth/login")
               .Content(new LoginModel {
                   Email = email,
                   Password = TestDomainLoginProvider.ValidPassword
               })
               .StatusCodeShouldBeOk()
            );

            Assert.NotNull(result);
            Assert.False(String.IsNullOrEmpty(result.Token));
        }

        [Fact]
        public async Task LoginValidNonExistantActiveDirectoryAsync() {
            Settings.Current.EnableActiveDirectoryAuth = true;

            var provider = new TestDomainLoginProvider();
            string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);

            await SendRequest(r => r
               .Post()
               .AppendPath("auth/login")
               .Content(new LoginModel {
                   Email = email,
                   Password = TestDomainLoginProvider.ValidPassword
               })
               .StatusCodeShouldBeUnauthorized()
            );
        }

        [Fact]
        public async Task LoginInvalidNonExistantActiveDirectoryAsync() {
            Settings.Current.EnableActiveDirectoryAuth = true;

            await SendRequest(r => r
               .Post()
               .AppendPath("auth/login")
               .Content(new LoginModel {
                   Email = TestDomainLoginProvider.ValidUsername + ".au",
                   Password = "Totallywrongpassword1234"
               })
               .StatusCodeShouldBeUnauthorized()
            );

            // Verify that a user account was not added
            var provider = new TestDomainLoginProvider();
            string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);
            var user = await _userRepository.GetByEmailAddressAsync(email + ".au");
            Assert.Null(user);
        }

        [Fact]
        public async Task LoginInvalidExistingActiveDirectoryAsync() {
            Settings.Current.EnableActiveDirectoryAuth = true;

            var provider = new TestDomainLoginProvider();
            string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);
            var user = new User {
                EmailAddress = email,
                IsEmailAddressVerified = true,
                FullName = "User 6"
            };
            await _userRepository.AddAsync(user, o => o.ImmediateConsistency());

            await SendRequest(r => r
               .Post()
               .AppendPath("auth/login")
               .Content(new LoginModel {
                   Email = TestDomainLoginProvider.ValidUsername,
                   Password = "Totallywrongpassword1234"
               })
               .StatusCodeShouldBeUnauthorized()
            );
        }

        private Task CreateOrganizationAndProjectsAsync() {
            return Task.WhenAll(
                _organizationRepository.AddAsync(OrganizationData.GenerateSampleOrganizations(), o => o.ImmediateConsistency()),
                _projectRepository.AddAsync(ProjectData.GenerateSampleProjects(), o => o.ImmediateConsistency())
            );
        }
    }
}