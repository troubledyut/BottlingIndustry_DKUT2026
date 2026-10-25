// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

using UnityEngine;

namespace realvirtual
{
	[AddComponentMenu("realvirtual/Motion/Drive Behaviors/Drive Erratic Position")]
	[RequireComponent(typeof(Drive))]
	//! This drive is only for test purposes. It is moving constantly two erratic positions between MinPos and MaxPos.
	[HelpURL("https://doc.realvirtual.io/components-and-scripts/motion/drive-behavior")]
	[Icon("Packages/io.realvirtual.starter/Editor/EditorAssets/EditorScriptIcons/Drive_ErraticPosition Icon.png")]
	public class Drive_ErraticPosition : BehaviorInterface, IDriveBehavior
	{
		[Tooltip("Minimum position in millimeters for random movement range")]
		public float MinPos = 0; //!< Minimum position of the range where the drive is allowed to move to.
		[Tooltip("Maximum position in millimeters for random movement range")]
		public float MaxPos = 100; //!< Maximum position of the range where the drive is allowed to move to.
		[Tooltip("Drive speed in mm/s for erratic movements")]
		public float Speed = 100; //!< Speed of the drive in millimeter / second.
		[Tooltip("Enable drive to move to random positions")]
		public bool Driving = false; //!< Set to true if Drive should drive to erratic positions.
		[Tooltip("Toggle between min/max only (true) or random positions (false)")]
		public bool IterateBetweenMaxAndMin = false; //!< If true, the drive will only iterate between MinPos and MaxPos. If false, the drive will iterate between random positions
		[Tooltip("PLC signal to enable erratic movement (always enabled if empty)")]
		public PLCOutputBool SignalEnable; //!< If this signal is true, the drive will drive to erratic positions. If the signal is void it is always enabled
		private Drive Drive;
		private float _destpos;
		private bool signalenablenotnull;
		private bool moveto0;
		private float positionTolerance = 0.01f; // tolerance for position comparison in mm
		void Reset()
		{
			Drive = GetComponent<Drive>();
			if (Drive.UseLimits)
			{
				MinPos = Drive.LowerLimit;
				MaxPos = Drive.UpperLimit;
			}
		}
		
		// Use this for initialization
		protected override void OnStartSim()
		{
			Drive = GetComponent<Drive>();
			signalenablenotnull = SignalEnable != null;
			moveto0 = false;
		}

		private void Start()
		{
			if (Drive == null)
				OnStartSim();
		}

		// Issues a positioning job via the Drive API. DriveTo sets the drive's internal start flag, so the
		// drive acknowledges the start pulse itself. Writing TargetStartMove directly from here would latch
		// it: a move shorter than one FixedUpdate never shows IsRunning==true, so the pulse would never be
		// cleared and the drive would ignore every later target (it only starts on a rising edge).
		private void StartMove(float target)
		{
			_destpos = target;
			Driving = true;
			Drive.TargetSpeed = Speed;
			Drive.DriveTo(target);
		}

		//! True once the drive has settled on the requested target.
		private bool ReachedDestination()
		{
			return Drive.IsAtTarget || Mathf.Abs(Drive.CurrentPosition - _destpos) <= positionTolerance;
		}

		// Update is called once per frame
		public void CalcFixedUpdate()
		{
			if (ForceStop || !this.enabled)
				return;

			// Handle return to zero when signal is disabled
			if (signalenablenotnull)
			{
				if (!SignalEnable.Value)
				{
					// Start the move back to zero once
					if (!moveto0 && Mathf.Abs(Drive.CurrentPosition) > positionTolerance)
					{
						moveto0 = true;
						StartMove(0);
						return;
					}

					if (moveto0 && ReachedDestination())
					{
						// Successfully reached zero - force exact position to avoid drift
						moveto0 = false;
						Driving = false;
						Drive.CurrentPosition = 0;
					}

					return; // Don't do erratic movement when signal is off
				}

				// Signal is enabled again - cancel a pending return to zero
				if (moveto0)
				{
					moveto0 = false;
					Driving = false;
				}
			}

			if (Driving)
			{
				if (ReachedDestination())
					Driving = false;
				return;
			}

			float nexttarget;
			if (!IterateBetweenMaxAndMin)
			{
				nexttarget = Random.Range(MinPos, MaxPos);
			}
			else
			{
				if (Mathf.Abs(Drive.CurrentPosition - MaxPos) <= positionTolerance)
					nexttarget = MinPos;
				else
					nexttarget = MaxPos;
			}

			StartMove(nexttarget);
		}
	}
}
