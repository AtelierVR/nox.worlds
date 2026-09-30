using Cysharp.Threading.Tasks;
using Nox.Search;
using Nox.Worlds.Runtime.Clients;

namespace Nox.Worlds.Runtime.Search {
	public class SearchData : IResultData {
		public Network.World Reference;

		public int Id
			=> Reference.Identifier.GetHashCode();

		public string[] TitleArguments
			=> new[] { Reference.Title ?? Reference.Identifier.ToString() };

		public UniTask<ImageSource> Image
			=> UniTask.FromResult(ImageSource.FromUrl(Reference.Thumbnail));

		public void OnClick(int menuId)
			=> Client.UiAPI?.SendGoto(menuId, WorldPage.GetStaticKey(), "world", Reference);
	}
}