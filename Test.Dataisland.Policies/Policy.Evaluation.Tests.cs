using Dataisland.Policies;

namespace Test.Dataisland.Policies
{
    [TestFixture]
    public class PolicyEvaluationTests
    {
        private readonly IPolicyEvaluation _policyEvaluation = new PolicyEvaluation();

        [Test]
        public void Evaluate_Should_ReturnAllow_When_ActionAndResourceMatch()
        {
            // Arrange
            var policies = new[]
            {
                new Policy("AllowRead", PolicyEffect.Allow, new PolicyAction[] { "s3:read" }, new PolicyResource[] { "document://123" })
            };
            var request = new PolicyRequest("s3:read", "document://123");

            // Act
            var result = _policyEvaluation.Evaluate(policies, request);

            // Assert
            Assert.That(result, Is.EqualTo(PolicyEffect.Allow));
        }

        [Test]
        public void Evaluate_Should_ReturnDeny_When_ActionAndResourceMatch()
        {
            // Arrange
            var policies = new[]
            {
                new Policy("DenyRead", PolicyEffect.Deny, new PolicyAction[] { "s3:read" }, new PolicyResource[] { "document://123" })
            };
            var request = new PolicyRequest("s3:read", "document://123");

            // Act
            var result = _policyEvaluation.Evaluate(policies, request);

            // Assert
            Assert.That(result, Is.EqualTo(PolicyEffect.Deny));
        }

        [Test]
        public void Evaluate_Should_ReturnDeny_When_NoPolicyMatches()
        {
            // Arrange
            var policies = new[]
            {
                new Policy("AllowWrite", PolicyEffect.Allow, new PolicyAction[] { "s3:write" }, new PolicyResource[] { "document://123" })
            };
            var request = new PolicyRequest("s3:read", "document://123");

            // Act
            var result = _policyEvaluation.Evaluate(policies, request);

            // Assert
            Assert.That(result, Is.EqualTo(PolicyEffect.Deny));
        }

        [Test]
        public void Evaluate_Should_Allow_WithWildcardAction()
        {
            // Arrange
            var policies = new[]
            {
                new Policy("AllowAllActions", PolicyEffect.Allow, new PolicyAction[] { "*" }, new PolicyResource[] { "document://123" })
            };
            var request = new PolicyRequest("s3:read", "document://123");

            // Act
            var result = _policyEvaluation.Evaluate(policies, request);

            // Assert
            Assert.That(result, Is.EqualTo(PolicyEffect.Allow));
        }

        [Test]
        public void Evaluate_Should_Allow_WithWildcardResource()
        {
            // Arrange
            var policies = new[]
            {
                new Policy("AllowAllResources", PolicyEffect.Allow, new PolicyAction[] { "s3:read" }, new PolicyResource[] { "*" })
            };
            var request = new PolicyRequest("s3:read", "document://123");

            // Act
            var result = _policyEvaluation.Evaluate(policies, request);

            // Assert
            Assert.That(result, Is.EqualTo(PolicyEffect.Allow));
        }

        [Test]
        public void Evaluate_Should_Deny_Overrides_Allow()
        {
            // Arrange
            var policies = new[]
            {
                new Policy("AllowRead", PolicyEffect.Allow, new PolicyAction[] { "s3:read" }, new PolicyResource[] { "document://123" }),
                new Policy("DenyRead", PolicyEffect.Deny, new PolicyAction[] { "s3:read" }, new PolicyResource[] { "document://123" })
            };
            var request = new PolicyRequest("s3:read", "document://123");

            // Act
            var result = _policyEvaluation.Evaluate(policies, request);

            // Assert
            Assert.That(result, Is.EqualTo(PolicyEffect.Deny));
        }

        [Test]
        public void Evaluate_Should_ReturnDeny_When_ActionMismatches()
        {
            // Arrange
            var policies = new[]
            {
                new Policy("AllowRead", PolicyEffect.Allow, new PolicyAction[] { "s3:read" }, new PolicyResource[] { "document://123" })
            };
            var request = new PolicyRequest("s3:write", "document://123");

            // Act
            var result = _policyEvaluation.Evaluate(policies, request);

            // Assert
            Assert.That(result, Is.EqualTo(PolicyEffect.Deny));
        }

        [Test]
        public void Evaluate_Should_ReturnDeny_When_ResourceMismatches()
        {
            // Arrange
            var policies = new[]
            {
                new Policy("AllowRead", PolicyEffect.Allow, new PolicyAction[] { "s3:read" }, new PolicyResource[] { "document://123" })
            };
            var request = new PolicyRequest("s3:read", "document://456");

            // Act
            var result = _policyEvaluation.Evaluate(policies, request);

            // Assert
            Assert.That(result, Is.EqualTo(PolicyEffect.Deny));
        }

        [Test]
        public void Evaluate_Should_Allow_WithPartialWildcardAction()
        {
            // Arrange
            var policies = new[]
            {
                new Policy("AllowS3Actions", PolicyEffect.Allow, new PolicyAction[] { "s3:*" }, new PolicyResource[] { "document://123" })
            };
            var request = new PolicyRequest("s3:read", "document://123");

            // Act
            var result = _policyEvaluation.Evaluate(policies, request);

            // Assert
            Assert.That(result, Is.EqualTo(PolicyEffect.Allow));
        }

        [Test]
        public void Evaluate_Should_Allow_WithPartialWildcardResource()
        {
            // Arrange
            var policies = new[]
            {
                new Policy("AllowDocumentResources", PolicyEffect.Allow, new PolicyAction[] { "s3:read" }, new PolicyResource[] { "document://*" })
            };
            var request = new PolicyRequest("s3:read", "document://123");

            // Act
            var result = _policyEvaluation.Evaluate(policies, request);

            // Assert
            Assert.That(result, Is.EqualTo(PolicyEffect.Allow));
        }
    }
}
