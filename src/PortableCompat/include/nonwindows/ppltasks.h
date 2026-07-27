#pragma once

#include <future>
#include <functional>
#include <type_traits>
#include <utility>

// API-compatible subset of Microsoft's Parallel Patterns Library used by the
// Calculator domain sources. Keeping the namespace, type, and continuation
// signatures intact allows UnitConverter.h/.cpp to compile byte-for-byte
// unchanged on non-Windows hosts.
namespace concurrency
{
    template<typename T>
    class task
    {
    public:
        task() = default;

        explicit task(std::future<T>&& future)
            : m_future(future.share())
        {
        }

        explicit task(std::shared_future<T> future)
            : m_future(std::move(future))
        {
        }

        T get() const
        {
            return m_future.get();
        }

        template<typename Continuation>
        auto then(Continuation&& continuation) const
        {
            using Result = std::invoke_result_t<std::decay_t<Continuation>, T>;
            auto previous = *this;
            return task<Result>(std::async(
                std::launch::async,
                [previous, continuation = std::forward<Continuation>(continuation)]() mutable -> Result {
                    return std::invoke(continuation, previous.get());
                }));
        }

    private:
        std::shared_future<T> m_future;
    };

    template<>
    class task<void>
    {
    public:
        task() = default;

        explicit task(std::future<void>&& future)
            : m_future(future.share())
        {
        }

        explicit task(std::shared_future<void> future)
            : m_future(std::move(future))
        {
        }

        void get() const
        {
            m_future.get();
        }

        template<typename Continuation>
        auto then(Continuation&& continuation) const
        {
            using Result = std::invoke_result_t<std::decay_t<Continuation>>;
            auto previous = *this;
            return task<Result>(std::async(
                std::launch::async,
                [previous, continuation = std::forward<Continuation>(continuation)]() mutable -> Result {
                    previous.get();
                    return std::invoke(continuation);
                }));
        }

    private:
        std::shared_future<void> m_future;
    };

    template<typename T>
    task<std::decay_t<T>> task_from_result(T&& value)
    {
        using Result = std::decay_t<T>;
        std::promise<Result> promise;
        promise.set_value(std::forward<T>(value));
        return task<Result>(promise.get_future());
    }

    inline task<void> task_from_result()
    {
        std::promise<void> promise;
        promise.set_value();
        return task<void>(promise.get_future());
    }
}
