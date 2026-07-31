// MIT license 
// 
// Copyright (c) 2017-2024 Advanced Micro Devices, Inc. All rights reserved.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

#pragma once

#include <fstream>
#include <string>

#include "public/include/core/Platform.h"

namespace amf
{
	// Utility class for tracking capture information 
	class CaptureStats
	{
	public:
		CaptureStats()
			: m_outfileFilename("")
			, m_summary("")
			, m_minDuration(MAXLONG)
			, m_maxDuration(0)
			, m_averageDuration(-1)
			, m_samples(0)
		{
		}

		~CaptureStats()
		{
		}

		void Init(const std::string& outputFilename, const std::string& summary)
		{
			m_outfileFilename = outputFilename;
			m_summary = summary;
		}

		void Reinit()
		{
			m_minDuration		=  MAXLONG;
			m_maxDuration		=  0;
			m_averageDuration	= -1;
			m_samples = 0;
		}

		void Terminate()
		{
			amf_pts tmp;
			std::ofstream file;
			file.open(m_outfileFilename);
			file << m_summary << std::endl;
			tmp = m_averageDuration / 10000; // to ms
			file << "\t" << "Average Duration (ms) " << tmp << std::endl;
			tmp = m_minDuration / 10000; // to ms
			file << "\t" << "Minimum Duration (ms) " << tmp << std::endl;
			tmp = m_maxDuration / 10000; // to ms
			file << "\t" << "Maximum Duration (ms) " << tmp << std::endl;
			file << "\t" << "Samples               " << m_samples << std::endl;
			file.close();
		}

		void addDuration(amf_pts duration)
		{
			// Filter any bad values
			if (duration <= 0)
			{
				return;
			}
			//
			if (duration < m_minDuration)
			{
				m_minDuration = duration;
			}
			if (duration > m_maxDuration)
			{
				m_maxDuration = duration;
			}
			//
			m_averageDuration = (m_averageDuration < 0) ? duration : (m_averageDuration + duration) / 2;
			m_samples++;
		}

	private:
		std::string m_outfileFilename;
		std::string m_summary;

		amf_pts m_minDuration;
		amf_pts m_maxDuration;
		amf_pts m_averageDuration;
		unsigned m_samples;
	};
}

// Define is turned on if the class is included.
#define WANT_CAPTURE_STATS
