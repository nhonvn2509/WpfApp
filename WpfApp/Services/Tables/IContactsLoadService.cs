using WpfApp.Models.Tables;
namespace WpfApp.Services.Tables;

public interface IContactsLoadService : ITableLoadService<ContactsQuery, ContactRow>
{
}
