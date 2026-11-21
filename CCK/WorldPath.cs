using System.Collections.Generic;
using System.Text;
using Nox.CCK.Utils;
using UnityEngine;

namespace Nox.CCK.Worlds {
	/// <summary>
	/// Stable identity of an object of a world: the CRC32 of its hierarchy path under the world boundary.
	/// <para>
	/// Only the names below the <see cref="Anchor"/> tagged <see cref="WORLD_TAG"/> are used. The scene and
	/// the runtime world instance above it are local to a client (the instance root is named after an id
	/// that changes on every load), so they can never be part of a value every client derives identically.
	/// </para>
	/// </summary>
	public static class WorldPath {
		/// <summary>Tag of the anchor enclosing a world.</summary>
		public const string WORLD_TAG = "world";

		/// <summary>Whether the transform is the root of the world its hierarchy belongs to.</summary>
		public static bool IsBoundary(Transform target)
			=> target && target.TryGetComponent<Anchor>(out var anchor)
				&& anchor.HasTag(WORLD_TAG);

		/// <summary>
		/// CRC32 of the hierarchy path of an object, the world boundary excluded.
		/// <para>
		/// Returns 0 when there is no transform, and never 0 otherwise: 0 is reserved for "no type".
		/// </para>
		/// </summary>
		public static uint DeriveType(Transform target) {
			if (!target)
				return 0;

			var builder = new StringBuilder();

			var chain = new Stack<Transform>();
			for (var t = target; t != null; t = t.parent) {
				if (IsBoundary(t))
					break;

				chain.Push(t);
			}

			while (chain.Count > 0)
				builder.Append('/').Append(chain.Pop().name);

			var hash = (uint)Hash.CRC32(builder.ToString());
			return hash == 0 ? 1u : hash;
		}
	}
}
