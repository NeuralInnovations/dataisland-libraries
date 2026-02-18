using Dataisland.Core.Domain.Entities;
using Dataisland.MongoDB;
using MongoDB.Driver;

namespace Dataisland.Core.Repositories;

public interface IIconRepository : IRepository
{
    Task<Icon?> GetByIdAsync(string id);
    Task<Icon> CreateAsync(Icon icon);
    Task DeleteAsync(string id);
}

public class IconRepository : Repository<Icon>, IIconRepository
{
    public IconRepository(IMongoDBProvider provider)
        : base("icons", provider) { }

    public async Task<Icon?> GetByIdAsync(string id) =>
        await Collection.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task<Icon> CreateAsync(Icon icon)
    {
        await Collection.InsertOneAsync(icon);
        return icon;
    }

    public async Task DeleteAsync(string id) =>
        await Collection.DeleteOneAsync(x => x.Id == id);
}
