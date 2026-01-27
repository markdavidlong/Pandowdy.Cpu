namespace Pandowdy.Cpu

open Pandowdy.Cpu.Pipelines

/// CPU execution engine using micro-op pipeline architecture
module Cpu =

    let Clock (variant: CpuVariant) (buffer: CpuStateBuffer) (bus: IPandowdyCpuBus) : bool =
        let current = buffer.Current

        // Check if CPU is halted (Stopped, Jammed, or Waiting)
        // If halted, return true (instruction complete) but don't advance PC
        // Note: Bypassed status means the CPU is still running (halt was bypassed)
        match current.Status with
        | CpuStatus.Stopped | CpuStatus.Jammed | CpuStatus.Waiting ->
            true
        | _ -> // Running or Bypassed - continue execution
            let prev = buffer.Prev

            if current.Pipeline.Length = 0 || current.PipelineIndex >= current.Pipeline.Length then
                let opcode = bus.CpuRead(current.PC)
                let pipelines = getPipelines variant
                current.Pipeline <- pipelines.[int opcode]
                current.PipelineIndex <- 0
                current.InstructionComplete <- false

            let microOp = current.Pipeline.[current.PipelineIndex]
            microOp.Invoke(prev, current, bus)
            current.PipelineIndex <- current.PipelineIndex + 1

            if current.InstructionComplete then
                buffer.SwapIfComplete()
                true
            else
                false

    let Step (variant: CpuVariant) (buffer: CpuStateBuffer) (bus: IPandowdyCpuBus) : int =
            let mutable cycles = 0
            let mutable complete = false
            while not complete do
                complete <- Clock variant buffer bus
                cycles <- cycles + 1
            cycles

    let Run (variant: CpuVariant) (buffer: CpuStateBuffer) (bus: IPandowdyCpuBus) (maxCycles: int) : int =
        let mutable cycles = 0
        while cycles < maxCycles do
            Clock variant buffer bus |> ignore
            cycles <- cycles + 1
        cycles

    let Reset (buffer: CpuStateBuffer) (bus: IPandowdyCpuBus) =
        buffer.Reset()
        buffer.LoadResetVector(bus)

    let CurrentOpcode (buffer: CpuStateBuffer) (bus: IPandowdyCpuBus) : byte =
        if buffer.Current.Pipeline.Length > 0 && buffer.Current.PipelineIndex > 0 then
            bus.CpuRead(buffer.Prev.PC)
        else
            bus.CpuRead(buffer.Current.PC)

    let CyclesRemaining (buffer: CpuStateBuffer) : int =
        buffer.Current.Pipeline.Length - buffer.Current.PipelineIndex
