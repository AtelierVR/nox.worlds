using Cysharp.Threading.Tasks;
using UnityEngine.Events;

namespace Nox.Worlds {
	/// <summary>
	/// Interface for caching processes.
	/// </summary>
	public interface ICaching {
		/// <summary>
		/// Cancels the caching process.
		/// </summary>
		public void Cancel();

		/// <summary>
		/// Starts the caching process.
		/// </summary>
		public UniTask Start();

		/// <summary>
		/// Indicates whether the caching process is currently running.
		/// </summary>
		public bool IsRunning { get; }

		/// <summary>
		/// Waits for the caching process to complete.
		/// </summary>
		/// <returns></returns>
		UniTask Wait();

		/// <summary>
		/// Event triggered when the progress of the caching process changes.
		/// </summary>
		public UnityEvent<float> OnProgressChanged { get; }

		/// <summary>
		/// Gets the current progress of the caching process (0.0 to 1.0).
		/// </summary>
		public float Progress { get; }
	}
}