using System;
using Moq;
using Vault.Interfaces;
using Vault.Models;

namespace Vault.Tests.RepositoryTests;

public class DocumentRepositoryTest
{
    [Fact]
    public async Task AddAsync_ShouldAddEntity()
    {
        //Arrange
        var mockRepo = new Mock<IVaultRepository<Document>>();
        var doc =  new Document{Id = "doc-1", Path = "/tmp/test.pdf"};

        //Act
        await mockRepo.Object.AddAsync(doc);

        //Assert
        mockRepo.Verify(r => r.AddAsync(It.IsAny<Document>()), Times.Once);
    }
}
