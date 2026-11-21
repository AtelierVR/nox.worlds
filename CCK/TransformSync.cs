using System;
using Nox.CCK.Entities;
using Nox.CCK.Sessions;
using Nox.CCK.Utils;
using Nox.Entities;
using Nox.Players;
using Nox.Sessions;
using UnityEngine;
using UnityEngine.Events;

namespace Nox.CCK.Worlds {
	/// <summary>Elements of the pose a <see cref="TransformSync"/> replicates.</summary>
	[Flags]
	public enum TransformSyncKind {
		/// <summary>Nothing is replicated.</summary>
		None = 0,

		/// <summary>World position.</summary>
		Position = 1 << 0,

		/// <summary>World rotation.</summary>
		Rotation = 1 << 1,

		/// <summary>Local scale.</summary>
		Scale = 1 << 2,

		/// <summary>Linear velocity, from the rigidbody.</summary>
		Velocity = 1 << 3,

		/// <summary>Angular velocity, from the rigidbody.</summary>
		Angular = 1 << 4,

		/// <summary>Everything a rigidbody carries.</summary>
		All = Position | Rotation | Scale | Velocity | Angular,
	}

	/// <summary>
	/// Synchronizes the movement of an object replicated by a session: the owner streams the pose and,
	/// with a rigidbody, the velocity and the angular velocity; the other clients apply them.
	/// <para>
	/// The component is the entity itself and its only part: a <see cref="IPart"/> of rig
	/// <see cref="EntityRig.Base"/>, so the replicated entity treats it like a player rig part (delta
	/// threshold, periodic keyframe) and a prop moves exactly like a player does.
	/// </para>
	/// <para>
	/// It also carries the ownership of the entity: nobody owning it means the session master owns it, so a
	/// master change or disconnection hands it over without anything to transfer.
	/// </para>
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class TransformSync : MonoBehaviour, ISessionEntity, IMultiPartEntity<IPart>, IPart, IEntityType {
		[SerializeField, Tooltip("Elements of the pose replicated with the other clients.")]
		private TransformSyncKind kind = TransformSyncKind.All;

		[SerializeField, Tooltip("Stable name of this object on the network. Empty: its path in the world is used, so the same object gets the same id on every client.")]
		private string key;

		private Rigidbody _body;
		private DataContainer _data;
		private DateTime _updated = DateTime.UtcNow;

		/// <summary>Id of the entity assigned by the network (0 until it is registered).</summary>
		public uint Id { get; set; }

		/// <summary>
		/// Stable identity of the object on the network: the CRC32 of its <see cref="key"/>, or of its path in the
		/// world (see <see cref="WorldPath.DeriveType"/>) when no key is authored. It is the same on every client
		/// whatever the scene was loaded from (the editor, an asset bundle), which is what lets each client map
		/// the spawn back onto its own copy.
		/// </summary>
		public uint Type {
			get {
				if (!string.IsNullOrEmpty(key)) {
					var keyed = (uint)Hash.CRC32(key);
					return keyed == 0 ? 1u : keyed;
				}

				return WorldPath.DeriveType(transform);
			}
		}

		/// <summary>Values of the entity that are not replicated, kept out of the replicated properties.</summary>
		public IDataContainer Data
			=> _data ??= new DataContainer();

		/// <summary>The rigidbody driving the pose, when the object has one.</summary>
		public Rigidbody Body
			=> _body;

		/// <summary>Elements of the pose replicated with the other clients.</summary>
		public TransformSyncKind Kind {
			get => kind;
			set => kind = value;
		}

		#region Session entity

		private ISession _session;
		private uint _ownerId = IOwnedEntity.MASTER_OWNER_ID;
		private readonly UnityEvent<IPlayer, IPlayer> _onOwnershipChanged = new();

		/// <summary>The session the entity is set up for, or <c>null</c> when it is not registered yet.</summary>
		public ISession Session
			=> _session;

		/// <summary>Set up the entity with the session that replicates it.</summary>
		public void Setup(ISession session)
			=> _session = session;

		/// <summary>
		/// The player owning this entity: the session master while it has no explicit owner (the default),
		/// and the master again when the player it was granted to is gone.
		/// </summary>
		public IPlayer Owner
			=> _ownerId == IOwnedEntity.MASTER_OWNER_ID
				? _session?.MasterPlayer
				: _session?.Entities?.GetEntity<IPlayer>(_ownerId) ?? _session?.MasterPlayer;

		/// <summary>The local player owns the entity: it streams the pose.</summary>
		public bool IsOwner
			=> Owner is { IsLocal: true };

		/// <summary>Invoked when the ownership of the entity changes (new owner, previous owner).</summary>
		public UnityEvent<IPlayer, IPlayer> OnOwnershipChanged
			=> _onOwnershipChanged;

		/// <summary>Apply an ownership decided by the session: no receiver means the session master.</summary>
		public void ApplyOwner(IPlayer receiver, IPlayer requester)
			=> _ownerId = receiver?.Id ?? IOwnedEntity.MASTER_OWNER_ID;

		/// <summary>The object is mirrored by the relay: its owner streams its pose.</summary>
		public bool IsNetworked
			=> true;

		/// <summary>The entity replicates a pose, it carries no replicated property.</summary>
		public IProperty[] Properties
			=> Array.Empty<IProperty>();

		public bool TryGetProperty(int key, out IProperty property) {
			property = null;
			return false;
		}

		public bool DefineProperty(string name, object value = null, PropertyFlags flags = PropertyFlags.Synced)
			=> false;

		public UnityEvent<IProperty> OnProperty { get; } = new();

		public void Tick() { }

		public void EmitEvent(long name, byte[] payload) { }

		public UnityEvent<long, byte[], uint> OnEvent { get; } = new();

		#endregion

		#region IMultiPartEntity

		/// <summary>The entity is its own only part.</summary>
		public IPart[] GetParts()
			=> new IPart[] { this };

		/// <summary>The entity has a single part, the base one.</summary>
		public bool TryGetPart(ushort id, out IPart part) {
			part = id == (ushort)EntityRig.Base
				? this
				: null;
			return part != null;
		}

		#endregion

		#region IPart

		/// <summary>Rig index of the part: the base part, always.</summary>
		ushort IPart.Id
			=> (ushort)EntityRig.Base;

		public bool IsDirty {
			get => false;
			set { }
		}

		public DateTime Updated
			=> _updated;

		public Vector3 Position {
			get => Target.position;
			set {
				if (!kind.HasFlag(TransformSyncKind.Position))
					return;

				Target.position = value;
				_updated        = DateTime.UtcNow;
			}
		}

		public Quaternion Rotation {
			get => Target.rotation;
			set {
				if (!kind.HasFlag(TransformSyncKind.Rotation))
					return;

				Target.rotation = value;
				_updated        = DateTime.UtcNow;
			}
		}

		public Vector3 Scale {
			get => Target.localScale;
			set {
				if (!kind.HasFlag(TransformSyncKind.Scale))
					return;

				Target.localScale = value;
				_updated          = DateTime.UtcNow;
			}
		}

		public Vector3 Velocity {
			get => _body && !_body.isKinematic ? _body.linearVelocity : Vector3.zero;
			set {
				if (!kind.HasFlag(TransformSyncKind.Velocity) || !_body || _body.isKinematic)
					return;

				_body.linearVelocity = value;
				_updated             = DateTime.UtcNow;
			}
		}

		public Vector3 Angular {
			get => _body && !_body.isKinematic ? _body.angularVelocity : Vector3.zero;
			set {
				if (!kind.HasFlag(TransformSyncKind.Angular) || !_body || _body.isKinematic)
					return;

				_body.angularVelocity = value;
				_updated              = DateTime.UtcNow;
			}
		}

		#endregion

		private Transform Target
			=> _body ? _body.transform : transform;

		private void Awake()
			=> _body = GetComponent<Rigidbody>();
	}
}
