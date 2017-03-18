﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TPLPipeline
{
	public abstract class BaseJob : IPipelineJob
	{
		private List<IPipelineJobElement> _Elements = new List<IPipelineJobElement>();
		private TaskCompletionSource<bool> CompletionTcs = new TaskCompletionSource<bool>();
		
		private object MergeLock { get; set; } = new object();
		private bool Merged { get; set; }

		public Task<bool> Completion => CompletionTcs.Task;
		public string Id { get; private set; }

		bool IPipelineJob.IsCompleted(string stepName)
		{
			return _Elements
				.Where(e => !e.Disabled && (e.CurrentStepName?.EndsWith(stepName) ?? false))
				.All(e => e.CompletedStepName?.EndsWith(stepName) ?? false);
		}
		bool IPipelineJob.IsCompleted(string stepName, Predicate<IPipelineJobElement> predicate)
		{
			return _Elements
				.Where(e => predicate(e) && !e.Disabled && (e.CurrentStepName?.EndsWith(stepName) ?? false))
				.All(e => e.CompletedStepName?.EndsWith(stepName) ?? false);
		}

		public abstract void OnJobStart();
		public abstract void OnJobComplete();

		public BaseJob()
		{
			Id = Guid.NewGuid().ToString();
		}

		void IPipelineJob.Complete(string stepName)
		{
			if (_Elements.TrueForAll(e => e.Disabled || (e.CompletedStepName?.EndsWith(stepName) ?? false)))
			{
				CompletionTcs.TrySetResult(true);
				OnJobComplete();
			}
		}

		IEnumerable<IPipelineJobElement> IPipelineJob.Elements()
		{
			return _Elements;
		}

		IEnumerable<IPipelineJobElement> IPipelineJob.MergeElements()
		{
			return ((IPipelineJob)this).MergeElements(e => true);
		}
		IEnumerable<IPipelineJobElement> IPipelineJob.MergeElements(Predicate<IPipelineJobElement> predicate)
		{
			lock (MergeLock)
			{
				if (!Merged)
				{
					Merged = true;
					var elements = _Elements.Where(e => predicate(e)).ToList();

					elements.GetRange(1, elements.Count - 1).ForEach(element => element.Disable());

					return elements;
				}
				else
				{
					return null;
				}
			}
		}

		IPipelineJobElement IPipelineJob.MergeToSingleElement(IEnumerable<IPipelineJobElement> elements)
		{
			var elementList = elements.ToList();

			foreach (var element in elementList.GetRange(1, elementList.Count - 1))
			{
				element.Disable();
			}
			return elementList.First();
		}

		IPipelineJobElement IPipelineJob.MergeToSingleElement<T1, T2>(Tuple<IPipelineJobElement, IPipelineJobElement> elements)
		{
			var newData = Tuple.Create(elements.Item1.GetData<T1>(), elements.Item2.GetData<T2>());

			elements.Item1.SetData(newData);
			elements.Item2.Disable();

			return elements.Item1;
		}

		public void AddData(object value)
		{
			_Elements.Add(new JobElement(this, _Elements.Count, value));
		}
		public void AddDataRange(IEnumerable<object> value)
		{
			_Elements.Add(new JobElement(this, _Elements.Count, value));
		}

	}
}
